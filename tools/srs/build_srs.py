#!/usr/bin/env python3
"""Build the code-based PRN222 SRS as a deterministic, styled DOCX."""

from __future__ import annotations

import argparse
import json
import re
import sys
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence

from docx import Document
from docx.document import Document as DocumentType
from docx.enum.section import WD_ORIENT, WD_SECTION
from docx.enum.style import WD_STYLE_TYPE
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_MARKDOWN = ROOT / "docs" / "srs" / "software-requirements-specification.md"
DEFAULT_REQUIREMENTS = ROOT / "docs" / "srs" / "requirements.json"
DEFAULT_DIAGRAMS = ROOT / "artifacts" / "srs-diagrams"
DEFAULT_OUTPUT = ROOT / "artifacts" / "PRN222_Software_Requirements_Specification.docx"

DOCUMENT_ID = "PRN222-RAG-SRS"
VERSION = "1.3"
REVISION_DATE = "June 15, 2026"
REVISION_DATE_ISO = "2026-06-15"
BASELINE = "7ae9ed8"
CONTENT_WIDTH_DXA = 9360
TABLE_INDENT_DXA = 120
CELL_MARGINS = {"top": 80, "bottom": 80, "start": 120, "end": 120}

BLUE = "2E74B5"
DARK_BLUE = "1F4D78"
INK = "0B2545"
MUTED = "667085"
LIGHT_GRAY = "F2F4F7"
BORDER = "B8C2CC"
WHITE = "FFFFFF"
PALE_BLUE = "EAF2F8"
PALE_GREEN = "EAF4EA"
PALE_GOLD = "FFF4D6"

REQUIREMENT_HEADING = re.compile(r"^(FR|NFR)-[A-Z]+-\d{3}\s+-\s+")
HEADING = re.compile(r"^(#{1,4})\s+(.+)$")
NUMBERED_ITEM = re.compile(r"^\s*(\d+)\.\s+(.+)$")
BULLET_ITEM = re.compile(r"^\s*-\s+(.+)$")
INLINE_TOKEN = re.compile(r"(`[^`]+`|\*\*[^*]+\*\*|\*[^*]+\*)")
REQ_ID = re.compile(r"^(FR|NFR)-[A-Z]+-\d{3}$")


@dataclass(frozen=True)
class Block:
    kind: str
    value: object


@dataclass(frozen=True)
class NumberingIds:
    headings: int
    bullet: int
    decimal: int


def set_cell_text(cell, text: str, *, bold: bool = False, color: str = "000000",
                  size: float = 9.5, align: WD_ALIGN_PARAGRAPH = WD_ALIGN_PARAGRAPH.LEFT) -> None:
    cell.text = ""
    paragraph = cell.paragraphs[0]
    paragraph.alignment = align
    paragraph.paragraph_format.space_before = Pt(0)
    paragraph.paragraph_format.space_after = Pt(0)
    paragraph.paragraph_format.line_spacing = 1.0
    run = paragraph.add_run(text)
    set_run(run, size=size, color=color, bold=bold)
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER


def set_run(run, *, name: str = "Calibri", size: float | None = None,
            color: str | None = None, bold: bool | None = None,
            italic: bool | None = None) -> None:
    run.font.name = name
    run._element.get_or_add_rPr().rFonts.set(qn("w:ascii"), name)
    run._element.get_or_add_rPr().rFonts.set(qn("w:hAnsi"), name)
    if size is not None:
        run.font.size = Pt(size)
    if color is not None:
        run.font.color.rgb = RGBColor.from_string(color)
    if bold is not None:
        run.bold = bold
    if italic is not None:
        run.italic = italic


def shade_cell(cell, fill: str) -> None:
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tc_pr.append(shd)
    shd.set(qn("w:fill"), fill)


def set_cell_margins(cell) -> None:
    tc_pr = cell._tc.get_or_add_tcPr()
    tc_mar = tc_pr.first_child_found_in("w:tcMar")
    if tc_mar is None:
        tc_mar = OxmlElement("w:tcMar")
        tc_pr.append(tc_mar)
    for side, value in CELL_MARGINS.items():
        node = tc_mar.find(qn(f"w:{side}"))
        if node is None:
            node = OxmlElement(f"w:{side}")
            tc_mar.append(node)
        node.set(qn("w:w"), str(value))
        node.set(qn("w:type"), "dxa")


def repeat_table_header(row) -> None:
    tr_pr = row._tr.get_or_add_trPr()
    header = OxmlElement("w:tblHeader")
    header.set(qn("w:val"), "true")
    tr_pr.append(header)


def next_numbering_id(numbering, tag: str) -> int:
    element_name = "abstractNum" if tag == "abstractNumId" else "num"
    values = [
        int(node.get(qn(f"w:{tag}")))
        for node in numbering.findall(qn(f"w:{element_name}"))
        if node.get(qn(f"w:{tag}")) is not None
    ]
    return max(values, default=0) + 1


def add_level(abstract_num, ilvl: int, *, fmt: str, text: str,
              left: int, hanging: int, start: int = 1) -> None:
    lvl = OxmlElement("w:lvl")
    lvl.set(qn("w:ilvl"), str(ilvl))
    start_node = OxmlElement("w:start")
    start_node.set(qn("w:val"), str(start))
    num_fmt = OxmlElement("w:numFmt")
    num_fmt.set(qn("w:val"), fmt)
    lvl_text = OxmlElement("w:lvlText")
    lvl_text.set(qn("w:val"), text)
    lvl_jc = OxmlElement("w:lvlJc")
    lvl_jc.set(qn("w:val"), "left")
    p_pr = OxmlElement("w:pPr")
    tabs = OxmlElement("w:tabs")
    tab = OxmlElement("w:tab")
    tab.set(qn("w:val"), "num")
    tab.set(qn("w:pos"), str(left))
    tabs.append(tab)
    ind = OxmlElement("w:ind")
    ind.set(qn("w:left"), str(left))
    ind.set(qn("w:hanging"), str(hanging))
    spacing = OxmlElement("w:spacing")
    spacing.set(qn("w:after"), "160")
    spacing.set(qn("w:line"), "280")
    spacing.set(qn("w:lineRule"), "auto")
    p_pr.extend([tabs, ind, spacing])
    lvl.extend([start_node, num_fmt, lvl_text, lvl_jc, p_pr])
    abstract_num.append(lvl)


def add_numbering_definitions(doc: DocumentType) -> NumberingIds:
    numbering = doc.part.numbering_part.element
    abstract_id = next_numbering_id(numbering, "abstractNumId")
    heading_abs = OxmlElement("w:abstractNum")
    heading_abs.set(qn("w:abstractNumId"), str(abstract_id))
    for ilvl, text in enumerate(["%1.", "%1.%2", "%1.%2.%3", "%1.%2.%3.%4"]):
        add_level(heading_abs, ilvl, fmt="decimal", text=text, left=360 * (ilvl + 1), hanging=270)
    numbering.append(heading_abs)
    heading_num_id = next_numbering_id(numbering, "numId")
    heading_num = OxmlElement("w:num")
    heading_num.set(qn("w:numId"), str(heading_num_id))
    heading_abs_id = OxmlElement("w:abstractNumId")
    heading_abs_id.set(qn("w:val"), str(abstract_id))
    heading_num.append(heading_abs_id)
    numbering.append(heading_num)

    bullet_abs_id = next_numbering_id(numbering, "abstractNumId")
    bullet_abs = OxmlElement("w:abstractNum")
    bullet_abs.set(qn("w:abstractNumId"), str(bullet_abs_id))
    add_level(bullet_abs, 0, fmt="bullet", text="•", left=720, hanging=360)
    add_level(bullet_abs, 1, fmt="bullet", text="o", left=1080, hanging=360)
    numbering.append(bullet_abs)
    bullet_num_id = next_numbering_id(numbering, "numId")
    bullet_num = OxmlElement("w:num")
    bullet_num.set(qn("w:numId"), str(bullet_num_id))
    bullet_abs_ref = OxmlElement("w:abstractNumId")
    bullet_abs_ref.set(qn("w:val"), str(bullet_abs_id))
    bullet_num.append(bullet_abs_ref)
    numbering.append(bullet_num)

    decimal_abs_id = next_numbering_id(numbering, "abstractNumId")
    decimal_abs = OxmlElement("w:abstractNum")
    decimal_abs.set(qn("w:abstractNumId"), str(decimal_abs_id))
    add_level(decimal_abs, 0, fmt="decimal", text="%1.", left=720, hanging=360)
    add_level(decimal_abs, 1, fmt="decimal", text="%2.", left=1080, hanging=360)
    numbering.append(decimal_abs)
    decimal_num_id = next_numbering_id(numbering, "numId")
    decimal_num = OxmlElement("w:num")
    decimal_num.set(qn("w:numId"), str(decimal_num_id))
    decimal_abs_ref = OxmlElement("w:abstractNumId")
    decimal_abs_ref.set(qn("w:val"), str(decimal_abs_id))
    decimal_num.append(decimal_abs_ref)
    numbering.append(decimal_num)
    return NumberingIds(headings=heading_num_id, bullet=bullet_num_id, decimal=decimal_num_id)


def apply_num_pr(paragraph, num_id: int, level: int) -> None:
    p_pr = paragraph._p.get_or_add_pPr()
    num_pr = p_pr.find(qn("w:numPr"))
    if num_pr is None:
        num_pr = OxmlElement("w:numPr")
        p_pr.append(num_pr)
    ilvl = num_pr.find(qn("w:ilvl"))
    if ilvl is None:
        ilvl = OxmlElement("w:ilvl")
        num_pr.append(ilvl)
    ilvl.set(qn("w:val"), str(level))
    num = num_pr.find(qn("w:numId"))
    if num is None:
        num = OxmlElement("w:numId")
        num_pr.append(num)
    num.set(qn("w:val"), str(num_id))


def apply_list_spacing(paragraph) -> None:
    p_pr = paragraph._p.get_or_add_pPr()
    spacing = p_pr.find(qn("w:spacing"))
    if spacing is None:
        spacing = OxmlElement("w:spacing")
        p_pr.append(spacing)
    spacing.set(qn("w:after"), "160")
    spacing.set(qn("w:line"), "280")
    spacing.set(qn("w:lineRule"), "auto")


def set_table_geometry(table, widths: Sequence[int], *, total_width: int | None = None) -> None:
    total = total_width or sum(widths)
    if sum(widths) != total:
        raise ValueError(f"Column widths {widths} do not total {total}")
    table.autofit = False
    table.alignment = WD_TABLE_ALIGNMENT.LEFT
    tbl_pr = table._tbl.tblPr

    tbl_w = tbl_pr.first_child_found_in("w:tblW")
    if tbl_w is None:
        tbl_w = OxmlElement("w:tblW")
        tbl_pr.append(tbl_w)
    tbl_w.set(qn("w:w"), str(total))
    tbl_w.set(qn("w:type"), "dxa")

    tbl_ind = tbl_pr.first_child_found_in("w:tblInd")
    if tbl_ind is None:
        tbl_ind = OxmlElement("w:tblInd")
        tbl_pr.append(tbl_ind)
    tbl_ind.set(qn("w:w"), str(TABLE_INDENT_DXA))
    tbl_ind.set(qn("w:type"), "dxa")

    layout = tbl_pr.first_child_found_in("w:tblLayout")
    if layout is None:
        layout = OxmlElement("w:tblLayout")
        tbl_pr.append(layout)
    layout.set(qn("w:type"), "fixed")

    grid = table._tbl.tblGrid
    for child in list(grid):
        grid.remove(child)
    for width in widths:
        col = OxmlElement("w:gridCol")
        col.set(qn("w:w"), str(width))
        grid.append(col)

    for row in table.rows:
        for index, cell in enumerate(row.cells):
            width = widths[min(index, len(widths) - 1)]
            tc_pr = cell._tc.get_or_add_tcPr()
            tc_w = tc_pr.first_child_found_in("w:tcW")
            if tc_w is None:
                tc_w = OxmlElement("w:tcW")
                tc_pr.append(tc_w)
            tc_w.set(qn("w:w"), str(width))
            tc_w.set(qn("w:type"), "dxa")
            set_cell_margins(cell)


def set_table_borders(table, color: str = BORDER, size: str = "4") -> None:
    tbl_pr = table._tbl.tblPr
    borders = tbl_pr.first_child_found_in("w:tblBorders")
    if borders is None:
        borders = OxmlElement("w:tblBorders")
        tbl_pr.append(borders)
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        tag = borders.find(qn(f"w:{edge}"))
        if tag is None:
            tag = OxmlElement(f"w:{edge}")
            borders.append(tag)
        tag.set(qn("w:val"), "single")
        tag.set(qn("w:sz"), size)
        tag.set(qn("w:color"), color)


def style_table(table, widths: Sequence[int], *, header: bool = True,
                total_width: int | None = None, font_size: float = 9.5) -> None:
    set_table_geometry(table, widths, total_width=total_width)
    set_table_borders(table)
    if header and table.rows:
        repeat_table_header(table.rows[0])
        for cell in table.rows[0].cells:
            shade_cell(cell, LIGHT_GRAY)
            for run in cell.paragraphs[0].runs:
                set_run(run, size=font_size, color=INK, bold=True)
    for row in table.rows[1 if header else 0:]:
        for cell in row.cells:
            for paragraph in cell.paragraphs:
                paragraph.paragraph_format.space_before = Pt(0)
                paragraph.paragraph_format.space_after = Pt(0)
                paragraph.paragraph_format.line_spacing = 1.0
                for run in paragraph.runs:
                    set_run(run, size=font_size)


def add_inline(paragraph, text: str, *, default_size: float = 11,
               default_color: str = "000000") -> None:
    position = 0
    for match in INLINE_TOKEN.finditer(text):
        if match.start() > position:
            run = paragraph.add_run(text[position:match.start()])
            set_run(run, size=default_size, color=default_color)
        token = match.group(0)
        if token.startswith("`"):
            run = paragraph.add_run(token[1:-1])
            set_run(run, name="Consolas", size=max(8.5, default_size - 1), color=DARK_BLUE)
        elif token.startswith("**"):
            run = paragraph.add_run(token[2:-2])
            set_run(run, size=default_size, color=default_color, bold=True)
        else:
            run = paragraph.add_run(token[1:-1])
            set_run(run, size=default_size, color=default_color, italic=True)
        position = match.end()
    if position < len(text):
        run = paragraph.add_run(text[position:])
        set_run(run, size=default_size, color=default_color)


def set_keep_with_next(paragraph) -> None:
    paragraph.paragraph_format.keep_with_next = True


def configure_styles(doc: DocumentType) -> None:
    styles = doc.styles
    normal = styles["Normal"]
    normal.font.name = "Calibri"
    normal._element.get_or_add_rPr().rFonts.set(qn("w:ascii"), "Calibri")
    normal._element.get_or_add_rPr().rFonts.set(qn("w:hAnsi"), "Calibri")
    normal.font.size = Pt(11)
    normal.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.LEFT
    normal.paragraph_format.space_before = Pt(0)
    normal.paragraph_format.space_after = Pt(6)
    normal.paragraph_format.line_spacing = 1.10

    heading_specs = {
        "Heading 1": (16, BLUE, 16, 8),
        "Heading 2": (13, BLUE, 12, 6),
        "Heading 3": (12, DARK_BLUE, 8, 4),
    }
    for name, (size, color, before, after) in heading_specs.items():
        style = styles[name]
        style.font.name = "Calibri"
        style._element.get_or_add_rPr().rFonts.set(qn("w:ascii"), "Calibri")
        style._element.get_or_add_rPr().rFonts.set(qn("w:hAnsi"), "Calibri")
        style.font.size = Pt(size)
        style.font.bold = True
        style.font.color.rgb = RGBColor.from_string(color)
        style.paragraph_format.space_before = Pt(before)
        style.paragraph_format.space_after = Pt(after)
        style.paragraph_format.keep_with_next = True

    if "Heading 4" not in styles:
        h4 = styles.add_style("Heading 4", WD_STYLE_TYPE.PARAGRAPH)
    else:
        h4 = styles["Heading 4"]
    h4.font.name = "Calibri"
    h4._element.get_or_add_rPr().rFonts.set(qn("w:ascii"), "Calibri")
    h4._element.get_or_add_rPr().rFonts.set(qn("w:hAnsi"), "Calibri")
    h4.font.size = Pt(11)
    h4.font.bold = True
    h4.font.color.rgb = RGBColor.from_string(DARK_BLUE)
    h4.paragraph_format.space_before = Pt(6)
    h4.paragraph_format.space_after = Pt(3)
    h4.paragraph_format.keep_with_next = True

    for style_name in ("Caption", "Header", "Footer"):
        style = styles[style_name]
        style.font.name = "Calibri"
        style._element.get_or_add_rPr().rFonts.set(qn("w:ascii"), "Calibri")
        style._element.get_or_add_rPr().rFonts.set(qn("w:hAnsi"), "Calibri")

    caption = styles["Caption"]
    caption.font.size = Pt(9)
    caption.font.italic = True
    caption.font.color.rgb = RGBColor.from_string(MUTED)
    caption.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
    caption.paragraph_format.space_before = Pt(3)
    caption.paragraph_format.space_after = Pt(8)
    caption.paragraph_format.keep_with_next = True

    if "Requirement Metadata" not in styles:
        req_style = styles.add_style("Requirement Metadata", WD_STYLE_TYPE.PARAGRAPH)
    else:
        req_style = styles["Requirement Metadata"]
    req_style.font.name = "Calibri"
    req_style.font.size = Pt(9)
    req_style.paragraph_format.space_before = Pt(0)
    req_style.paragraph_format.space_after = Pt(2)
    req_style.paragraph_format.line_spacing = 1.0


def strip_literal_heading_number(text: str) -> str:
    return re.sub(r"^\d+(?:\.\d+)*\.?\s+", "", text)


def add_numbered_heading(doc: DocumentType, text: str, style_level: int,
                         numbering: NumberingIds):
    display_text = strip_literal_heading_number(text)
    paragraph = doc.add_paragraph(display_text, style=f"Heading {style_level}")
    apply_num_pr(paragraph, numbering.headings, max(0, style_level - 1))
    return paragraph


def configure_page(section, *, landscape: bool = False) -> None:
    section.orientation = WD_ORIENT.LANDSCAPE if landscape else WD_ORIENT.PORTRAIT
    if landscape:
        section.page_width = Inches(11)
        section.page_height = Inches(8.5)
    else:
        section.page_width = Inches(8.5)
        section.page_height = Inches(11)
    section.top_margin = Inches(1)
    section.bottom_margin = Inches(1)
    section.left_margin = Inches(1)
    section.right_margin = Inches(1)
    section.header_distance = Inches(0.492)
    section.footer_distance = Inches(0.492)


def add_field(paragraph, instruction: str, display: str = "") -> None:
    begin = OxmlElement("w:fldChar")
    begin.set(qn("w:fldCharType"), "begin")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = instruction
    separate = OxmlElement("w:fldChar")
    separate.set(qn("w:fldCharType"), "separate")
    text = OxmlElement("w:t")
    text.text = display
    end = OxmlElement("w:fldChar")
    end.set(qn("w:fldCharType"), "end")
    run = paragraph.add_run()
    run._r.extend([begin, instr, separate, text, end])


def configure_header_footer(section, *, first_page: bool = False) -> None:
    section.different_first_page_header_footer = first_page
    header = section.header
    header.is_linked_to_previous = False
    p = header.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p.paragraph_format.space_after = Pt(0)
    left = p.add_run("PRN222 Software Requirements Specification")
    set_run(left, size=8.5, color=MUTED, bold=True)
    p.add_run("\t")
    right = p.add_run(f"{DOCUMENT_ID} | Version {VERSION}")
    set_run(right, size=8.5, color=MUTED)
    tabs = p.paragraph_format.tab_stops
    tabs.add_tab_stop(Inches(6.5), alignment=2)

    footer = section.footer
    footer.is_linked_to_previous = False
    fp = footer.paragraphs[0]
    fp.alignment = WD_ALIGN_PARAGRAPH.CENTER
    fp.paragraph_format.space_before = Pt(0)
    fp.paragraph_format.space_after = Pt(0)
    run = fp.add_run(f"{DOCUMENT_ID} | Page ")
    set_run(run, size=8.5, color=MUTED)
    add_field(fp, "PAGE", "1")


def add_cover(doc: DocumentType) -> None:
    section = doc.sections[0]
    configure_page(section)
    configure_header_footer(section, first_page=True)

    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(78)
    p.paragraph_format.space_after = Pt(10)
    r = p.add_run("SOFTWARE REQUIREMENTS SPECIFICATION")
    set_run(r, size=12, color=BLUE, bold=True)

    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(8)
    r = p.add_run("PRN222 RAG Workbench")
    set_run(r, size=30, color=INK, bold=True)

    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(28)
    r = p.add_run(
        "A course-scoped retrieval-augmented generation platform for Vietnamese "
        "academic documents, evaluation, and dataset preparation"
    )
    set_run(r, size=14, color=DARK_BLUE)

    meta = doc.add_table(rows=4, cols=2)
    entries = [
        ("Document ID", DOCUMENT_ID),
        ("Version", VERSION),
        ("Revision date", REVISION_DATE),
        ("Source baseline", BASELINE),
    ]
    for row, (label, value) in zip(meta.rows, entries):
        set_cell_text(row.cells[0], label, bold=True, color=DARK_BLUE, size=10)
        set_cell_text(row.cells[1], value, color=INK, size=10)
        shade_cell(row.cells[0], LIGHT_GRAY)
    style_table(meta, [2160, 7200], header=False, font_size=10)

    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(42)
    p.paragraph_format.space_after = Pt(4)
    r = p.add_run("ACADEMIC SUBMISSION")
    set_run(r, size=10, color=BLUE, bold=True)
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(4)
    r = p.add_run("PRN222 - .NET Web Development")
    set_run(r, size=12, color=INK, bold=True)
    p = doc.add_paragraph()
    r = p.add_run("Prepared from implemented repository evidence and a validated requirement catalog")
    set_run(r, size=10, color=MUTED, italic=True)

    doc.add_page_break()


def add_front_matter(doc: DocumentType) -> None:
    heading = doc.add_paragraph("Document Control", style="Heading 1")
    heading.paragraph_format.space_before = Pt(0)
    rows = [
        ("Document identifier", DOCUMENT_ID),
        ("Document title", "Software Requirements Specification for PRN222 RAG Workbench"),
        ("Version", VERSION),
        ("Revision date", REVISION_DATE_ISO),
        ("Status", "Draft for academic submission"),
        ("Source baseline", f"Implementation repository branch main through commit {BASELINE}"),
        ("Primary source of truth", "Implemented source code, validated requirements, and source evidence"),
    ]
    table = doc.add_table(rows=1, cols=2)
    set_cell_text(table.rows[0].cells[0], "Field", bold=True, color=INK)
    set_cell_text(table.rows[0].cells[1], "Value", bold=True, color=INK)
    for label, value in rows:
        cells = table.add_row().cells
        set_cell_text(cells[0], label, bold=True, color=DARK_BLUE)
        set_cell_text(cells[1], value)
    style_table(table, [2700, 6660])

    doc.add_paragraph("Revision History", style="Heading 2")
    table = doc.add_table(rows=4, cols=3)
    for index, value in enumerate(("Version", "Date", "Description")):
        set_cell_text(table.rows[0].cells[index], value, bold=True, color=INK)
    for index, value in enumerate(("1.0", "2026-06-07", "Initial code-based SRS for academic submission.")):
        set_cell_text(table.rows[1].cells[index], value)
    for index, value in enumerate(("1.1", "2026-06-09", "Finalized traceability, diagrams, document generation, and submission metadata.")):
        set_cell_text(table.rows[2].cells[index], value)
    for index, value in enumerate(("1.2", REVISION_DATE_ISO, "Merged SRS sources into main and aligned login-page Lecturer copy with enforced authorization.")):
        set_cell_text(table.rows[3].cells[index], value)
    style_table(table, [1200, 1800, 6360])

    doc.add_page_break()
    doc.add_paragraph("Table of Contents", style="Heading 1")
    toc = doc.add_paragraph()
    toc.paragraph_format.space_after = Pt(6)
    add_field(toc, 'TOC \\o "1-4" \\h \\z \\u', "Right-click and select Update Field to refresh the table of contents.")
    note = doc.add_paragraph()
    run = note.add_run("Note: Word is configured to update fields when this document is opened.")
    set_run(run, size=9, color=MUTED, italic=True)
    doc.add_page_break()


def parse_markdown(text: str) -> list[Block]:
    lines = text.splitlines()
    blocks: list[Block] = []
    index = 0
    while index < len(lines):
        line = lines[index].rstrip()
        if not line:
            index += 1
            continue
        heading = HEADING.match(line)
        if heading:
            blocks.append(Block("heading", (len(heading.group(1)), heading.group(2).strip())))
            index += 1
            continue
        if line.startswith("|"):
            rows = []
            while index < len(lines) and lines[index].lstrip().startswith("|"):
                cells = [cell.strip() for cell in lines[index].strip().strip("|").split("|")]
                rows.append(cells)
                index += 1
            if len(rows) >= 2 and all(re.fullmatch(r":?-{3,}:?", cell) for cell in rows[1]):
                rows.pop(1)
            blocks.append(Block("table", rows))
            continue
        numbered = NUMBERED_ITEM.match(line)
        if numbered:
            items = []
            while index < len(lines):
                match = NUMBERED_ITEM.match(lines[index])
                if not match:
                    break
                items.append(match.group(2).strip())
                index += 1
            blocks.append(Block("numbered", items))
            continue
        bullet = BULLET_ITEM.match(line)
        if bullet:
            items = []
            while index < len(lines):
                match = BULLET_ITEM.match(lines[index])
                if not match:
                    break
                item = match.group(1).strip()
                nested = []
                index += 1
                while index < len(lines) and NUMBERED_ITEM.match(lines[index]):
                    nested.append(NUMBERED_ITEM.match(lines[index]).group(2).strip())
                    index += 1
                items.append((item, nested))
            blocks.append(Block("bullets", items))
            continue
        paragraphs = [line]
        index += 1
        while index < len(lines):
            candidate = lines[index].rstrip()
            if not candidate or HEADING.match(candidate) or candidate.startswith("|") or \
                    NUMBERED_ITEM.match(candidate) or BULLET_ITEM.match(candidate):
                break
            paragraphs.append(candidate.strip())
            index += 1
        blocks.append(Block("paragraph", " ".join(paragraphs)))
    return blocks


def add_real_list_item(doc: DocumentType, text: str, numbering: NumberingIds, *,
                       numbered: bool = False, level: int = 0):
    style = "List Number" if numbered else "List Bullet"
    p = doc.add_paragraph(style=style)
    p.paragraph_format.left_indent = Inches(0.5 + level * 0.25)
    p.paragraph_format.first_line_indent = Inches(-0.25)
    p.paragraph_format.space_after = Pt(8)
    p.paragraph_format.line_spacing = 1.167
    apply_num_pr(p, numbering.decimal if numbered else numbering.bullet, level)
    apply_list_spacing(p)
    add_inline(p, text)
    return p


def add_markdown_table(doc: DocumentType, rows: Sequence[Sequence[str]], heading_text: str,
                       *, landscape: bool = False) -> None:
    if not rows:
        return
    cols = max(len(row) for row in rows)
    table = doc.add_table(rows=1, cols=cols)
    for index, value in enumerate(rows[0]):
        set_cell_text(table.rows[0].cells[index], value, bold=True, color=INK, size=8.5 if landscape else 9)
    for source_row in rows[1:]:
        cells = table.add_row().cells
        for index in range(cols):
            set_cell_text(cells[index], source_row[index] if index < len(source_row) else "",
                          size=8 if landscape else 9)

    if landscape:
        widths = [1500, 2400, 6100, 1520] if cols == 4 else [11520 // cols] * cols
        style_table(table, widths, total_width=11520, font_size=8)
    elif cols == 2:
        widths = [2500, 6860]
        if "Reference" in heading_text:
            widths = [2900, 6460]
        style_table(table, widths, font_size=9)
    elif cols == 3:
        style_table(table, [1500, 1900, 5960], font_size=9)
    elif cols == 5:
        style_table(table, [3900, 1365, 1365, 1365, 1365], font_size=8.5)
    elif cols == 4:
        style_table(table, [2300, 2300, 2380, 2380], font_size=8.5)
    else:
        base = CONTENT_WIDTH_DXA // cols
        widths = [base] * cols
        widths[-1] += CONTENT_WIDTH_DXA - sum(widths)
        style_table(table, widths, font_size=8.5)


def add_diagram(doc: DocumentType, image_path: Path, caption: str, width: float) -> None:
    if not image_path.exists():
        raise FileNotFoundError(f"Missing diagram: {image_path}")
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.keep_with_next = True
    run = p.add_run()
    run.add_picture(str(image_path), width=Inches(width))
    cap = doc.add_paragraph(caption, style="Caption")
    cap.paragraph_format.keep_with_next = False


def requirement_status_fill(status: str) -> str:
    if status == "Implemented":
        return PALE_GREEN
    if status in {"Target", "Future"}:
        return PALE_GOLD
    return PALE_BLUE


def add_requirement_block(doc: DocumentType, requirement: dict, numbering: NumberingIds) -> None:
    table = doc.add_table(rows=6, cols=4)
    for index, value in enumerate(("Field", "Value", "Field", "Value")):
        set_cell_text(table.rows[0].cells[index], value, bold=True, color=INK, size=8.5)
    pairs = [
        ("Requirement ID", requirement["id"], "Title", requirement["title"]),
        ("Type", requirement["type"], "Priority", requirement["priority"]),
        ("Roles", ", ".join(requirement["roles"]), "Verification", requirement["verification"]),
        ("Status", requirement["status"], "Trace", ", ".join(requirement["tracesTo"])),
        ("Source", "; ".join(requirement["source"]), "", ""),
    ]
    for row_index, values in enumerate(pairs):
        for col_index, value in enumerate(values):
            set_cell_text(
                table.rows[row_index + 1].cells[col_index],
                value,
                bold=col_index % 2 == 0 and bool(value),
                color=DARK_BLUE if col_index % 2 == 0 else "000000",
                size=8.5,
            )
            if col_index % 2 == 0 and value:
                shade_cell(table.rows[row_index + 1].cells[col_index], LIGHT_GRAY)
    table.rows[5].cells[1].merge(table.rows[5].cells[3])
    style_table(table, [1320, 3360, 1320, 3360], header=True, font_size=8.5)
    shade_cell(table.rows[4].cells[1], requirement_status_fill(requirement["status"]))

    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(3)
    p.paragraph_format.space_after = Pt(3)
    label = p.add_run("Statement. ")
    set_run(label, size=10, color=DARK_BLUE, bold=True)
    add_inline(p, requirement["statement"], default_size=10)

    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(2)
    label = p.add_run("Acceptance criteria")
    set_run(label, size=9.5, color=DARK_BLUE, bold=True)
    for criterion in requirement["acceptanceCriteria"]:
        add_real_list_item(doc, criterion, numbering)


def add_body(doc: DocumentType, markdown: str, requirements: Sequence[dict],
             diagrams_dir: Path, numbering: NumberingIds) -> None:
    requirement_by_id = {item["id"]: item for item in requirements}
    blocks = parse_markdown(markdown)
    skip_document_control = True
    current_heading = ""
    landscape = False
    inserted = set()

    for block in blocks:
        if block.kind == "heading":
            level, text = block.value
            if level == 1:
                continue
            if skip_document_control:
                if text == "1. Introduction":
                    skip_document_control = False
                else:
                    continue

            if text == "8.4 Requirements Traceability Matrix" and not landscape:
                section = doc.add_section(WD_SECTION.NEW_PAGE)
                configure_page(section, landscape=True)
                configure_header_footer(section)
                landscape = True
            elif landscape and text != "8.4 Requirements Traceability Matrix":
                section = doc.add_section(WD_SECTION.NEW_PAGE)
                configure_page(section, landscape=False)
                configure_header_footer(section)
                landscape = False

            current_heading = text
            style_level = min(level - 1, 4)
            paragraph = add_numbered_heading(doc, text, style_level, numbering)
            if REQUIREMENT_HEADING.match(text):
                requirement_id = text.split(" - ", 1)[0]
                req = requirement_by_id.get(requirement_id)
                if req is None:
                    raise ValueError(f"Requirement heading missing from catalog: {requirement_id}")
                add_requirement_block(doc, req, numbering)
                inserted.add(requirement_id)

            if text == "3.1 System Context":
                add_diagram(doc, diagrams_dir / "system-context.png",
                            "Figure 1. PRN222 RAG Workbench system context.", 6.35)
            elif text == "4.4 Document Ingestion and Knowledge Base Management":
                add_diagram(doc, diagrams_dir / "rag-processing-flow.png",
                            "Figure 2. Document ingestion and RAG query processing flow.", 6.35)
            elif text == "6.1 Core Entities":
                add_diagram(doc, diagrams_dir / "domain-model.png",
                            "Figure 3. Core data domain model.", 6.35)
            continue

        if skip_document_control:
            continue
        if block.kind == "paragraph":
            p = doc.add_paragraph()
            add_inline(p, str(block.value))
        elif block.kind == "table":
            add_markdown_table(doc, block.value, current_heading, landscape=landscape)
        elif block.kind == "numbered":
            for item in block.value:
                add_real_list_item(doc, item, numbering, numbered=True)
        elif block.kind == "bullets":
            for item, nested in block.value:
                # Requirement content is generated from JSON to prevent drift.
                if REQUIREMENT_HEADING.match(current_heading):
                    continue
                add_real_list_item(doc, item, numbering)
                for child in nested:
                    add_real_list_item(doc, child, numbering, numbered=True, level=1)

    missing = set(requirement_by_id) - inserted
    if missing:
        raise ValueError(f"Requirements not rendered from Markdown headings: {sorted(missing)}")


def set_document_properties(doc: DocumentType) -> None:
    props = doc.core_properties
    props.title = "Software Requirements Specification for PRN222 RAG Workbench"
    props.subject = "Code-based academic software requirements specification"
    props.author = "PRN222 Project Team"
    props.keywords = "SRS, PRN222, RAG, ASP.NET Core, requirements"
    props.comments = "Generated deterministically from repository-backed Markdown and JSON inputs."

    settings = doc.settings._element
    update_fields = settings.find(qn("w:updateFields"))
    if update_fields is None:
        update_fields = OxmlElement("w:updateFields")
        settings.append(update_fields)
    update_fields.set(qn("w:val"), "true")


def build(markdown_path: Path, requirements_path: Path, diagrams_dir: Path,
          output_path: Path) -> None:
    markdown = markdown_path.read_text(encoding="utf-8")
    requirements = json.loads(requirements_path.read_text(encoding="utf-8"))
    if not isinstance(requirements, list) or not requirements:
        raise ValueError("Requirement catalog must be a non-empty JSON array")

    doc = Document()
    configure_styles(doc)
    numbering = add_numbering_definitions(doc)
    configure_page(doc.sections[0])
    set_document_properties(doc)
    add_cover(doc)
    add_front_matter(doc)
    add_body(doc, markdown, requirements, diagrams_dir, numbering)

    for section in doc.sections:
        if not section.header.paragraphs[0].text:
            configure_header_footer(section)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    doc.save(output_path)


def structural_checks(output_path: Path, requirements_path: Path) -> list[str]:
    requirements = json.loads(requirements_path.read_text(encoding="utf-8"))
    doc = Document(output_path)
    text = "\n".join(p.text for p in doc.paragraphs)
    table_text = "\n".join(cell.text for table in doc.tables for row in table.rows for cell in row.cells)
    all_text = text + "\n" + table_text
    checks = []

    required_text = [
        "PRN222 RAG Workbench",
        "Introduction",
        "Overall Description",
        "System Context and Authorization Model",
        "Functional Requirements",
        "External Interface Requirements",
        "Data Requirements",
        "Non-Functional Requirements",
        "Verification, Use Cases, and Traceability",
        "Limitations and Future Scope",
        "References",
        VERSION,
        REVISION_DATE_ISO,
        BASELINE,
    ]
    missing_sections = [item for item in required_text if item not in all_text]
    if missing_sections:
        raise AssertionError(f"Missing mandatory text: {missing_sections}")
    checks.append("mandatory title and top-level sections present")

    requirement_block_counts = {item["id"]: 0 for item in requirements}
    repeated_requirement_headers = 0
    for table in doc.tables:
        first_row = [cell.text.strip() for cell in table.rows[0].cells]
        if first_row == ["Field", "Value", "Field", "Value"]:
            tr_pr = table.rows[0]._tr.get_or_add_trPr()
            if tr_pr.find(qn("w:tblHeader")) is None:
                raise AssertionError("Requirement metadata table missing repeated header row")
            repeated_requirement_headers += 1
            for row in table.rows[1:]:
                cells = [cell.text.strip() for cell in row.cells]
                for index in range(0, min(len(cells), 4), 2):
                    if cells[index] == "Requirement ID" and REQ_ID.match(cells[index + 1] if index + 1 < len(cells) else ""):
                        requirement_block_counts[cells[index + 1]] += 1
    wrong_counts = {key: value for key, value in requirement_block_counts.items() if value != 1}
    if wrong_counts:
        raise AssertionError(f"Requirement IDs not exactly once in catalog blocks: {wrong_counts}")
    if repeated_requirement_headers != len(requirements):
        raise AssertionError(f"Expected {len(requirements)} repeated requirement headers, found {repeated_requirement_headers}")
    checks.append(f"all {len(requirements)} requirement IDs appear exactly once in requirement metadata blocks")
    checks.append(f"{repeated_requirement_headers} requirement metadata tables have repeated header rows")

    with zipfile.ZipFile(output_path) as archive:
        names = archive.namelist()
        media = [name for name in names if name.startswith("word/media/")]
        if len(media) < 3:
            raise AssertionError(f"Expected at least 3 embedded images, found {len(media)}")
        if not any(name.startswith("word/header") for name in names):
            raise AssertionError("No header part found")
        if not any(name.startswith("word/footer") for name in names):
            raise AssertionError("No footer part found")
        document_xml = archive.read("word/document.xml").decode("utf-8")
        if 'TOC \\o "1-4"' not in document_xml:
            raise AssertionError("TOC field not found")
        if "w:numPr" not in document_xml:
            raise AssertionError("No numbering properties found")
        settings_xml = archive.read("word/settings.xml").decode("utf-8")
        if "updateFields" not in settings_xml:
            raise AssertionError("updateFields setting not found")
    checks.append(f"{len(media)} diagrams/images embedded")
    checks.append("header and footer parts present")
    checks.append("TOC field and updateFields setting present")
    heading_without_num = []
    for paragraph in doc.paragraphs:
        style = paragraph.style.name if paragraph.style is not None else ""
        if style.startswith("Heading") and paragraph.text not in {"Document Control", "Table of Contents", "Revision History"}:
            if paragraph._p.get_or_add_pPr().find(qn("w:numPr")) is None:
                heading_without_num.append(paragraph.text)
    if heading_without_num:
        raise AssertionError(f"Heading paragraphs without w:numPr: {heading_without_num[:10]}")
    checks.append("body heading paragraphs carry real Word numbering properties")

    empty_rows = 0
    geometry_errors = []
    for table_index, table in enumerate(doc.tables):
        for row in table.rows:
            if not any(cell.text.strip() for cell in row.cells):
                empty_rows += 1
        tbl_pr = table._tbl.tblPr
        tbl_w = tbl_pr.first_child_found_in("w:tblW")
        tbl_ind = tbl_pr.first_child_found_in("w:tblInd")
        grid = [int(node.get(qn("w:w"))) for node in table._tbl.tblGrid]
        if tbl_w is None or tbl_ind is None or not grid:
            geometry_errors.append(table_index)
    if empty_rows:
        raise AssertionError(f"Found {empty_rows} empty layout rows")
    if geometry_errors:
        raise AssertionError(f"Tables missing explicit geometry: {geometry_errors}")
    checks.append("no empty layout rows")
    checks.append(f"explicit geometry present on all {len(doc.tables)} tables")
    list_spacing_errors = 0
    for paragraph in doc.paragraphs:
        p_pr = paragraph._p.get_or_add_pPr()
        num_pr = p_pr.find(qn("w:numPr"))
        if num_pr is None or paragraph.style.name.startswith("Heading"):
            continue
        spacing = p_pr.find(qn("w:spacing"))
        if spacing is None or spacing.get(qn("w:after")) != "160" or spacing.get(qn("w:line")) != "280":
            list_spacing_errors += 1
    if list_spacing_errors:
        raise AssertionError(f"List paragraphs without standard_business_brief spacing: {list_spacing_errors}")
    checks.append("bullet and decimal list paragraphs use 8pt after and 1.167 line spacing")
    return checks


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--markdown", "--source", dest="markdown", type=Path, default=DEFAULT_MARKDOWN)
    parser.add_argument("--requirements", type=Path, default=DEFAULT_REQUIREMENTS)
    parser.add_argument("--diagrams", "--diagram-dir", dest="diagrams", type=Path, default=DEFAULT_DIAGRAMS)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--check-only", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        if not args.check_only:
            build(args.markdown, args.requirements, args.diagrams, args.output)
        checks = structural_checks(args.output, args.requirements)
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1

    print(f"PASS: {args.output}")
    print(f"SIZE: {args.output.stat().st_size} bytes")
    for check in checks:
        print(f"CHECK: {check}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
