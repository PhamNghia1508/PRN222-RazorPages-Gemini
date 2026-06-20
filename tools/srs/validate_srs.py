#!/usr/bin/env python3
"""Validate the machine-readable SRS requirement catalog."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from typing import Any


ID_PATTERN = re.compile(r"^(FR|NFR)-[A-Z]+-\d{3}$")
MANDATORY_FIELDS = (
    "id",
    "title",
    "statement",
    "type",
    "priority",
    "roles",
    "source",
    "verification",
    "acceptanceCriteria",
    "status",
    "tracesTo",
)
REQUIRED_FIELDS = set(MANDATORY_FIELDS)
ALLOWED_VALUES = {
    "type": {"functional", "non-functional"},
    "priority": {"Must", "Should", "Could"},
    "verification": {"Test", "Inspection", "Analysis", "Demonstration"},
    "status": {"Implemented", "Partially Implemented", "Target", "Future"},
}
ALLOWED_ROLES = {"Admin", "HeadLecturer", "Lecturer", "Student"}
STRING_FIELDS = {"id", "title", "statement", "type", "priority", "verification", "status"}
LIST_FIELDS = {"roles", "source", "acceptanceCriteria", "tracesTo"}


def is_empty(value: Any) -> bool:
    if value is None:
        return True
    if isinstance(value, str):
        return not value.strip()
    if isinstance(value, list):
        return not value
    return False


def validate_catalog(catalog_path: Path, repository_root: Path) -> list[str]:
    errors: list[str] = []
    try:
        data = json.loads(catalog_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        return [f"catalog: unable to read valid JSON: {exc}"]

    if not isinstance(data, list):
        return ["catalog: root value must be a JSON array"]

    seen_ids: set[str] = set()
    for index, requirement in enumerate(data):
        label = f"record[{index}]"
        if not isinstance(requirement, dict):
            errors.append(f"{label}: requirement must be a JSON object")
            continue

        unknown_fields = set(requirement) - REQUIRED_FIELDS
        if unknown_fields:
            fields = ", ".join(sorted(unknown_fields))
            errors.append(f"{label}: unknown field(s): {fields}")

        requirement_id = requirement.get("id")
        if isinstance(requirement_id, str) and requirement_id.strip():
            label = requirement_id
            if requirement_id in seen_ids:
                errors.append(f"{label}: duplicate requirement ID")
            seen_ids.add(requirement_id)
            if not ID_PATTERN.fullmatch(requirement_id):
                errors.append(
                    f"{label}: ID must match ^(FR|NFR)-[A-Z]+-\\d{{3}}$"
                )

        for field in MANDATORY_FIELDS:
            if field not in requirement or is_empty(requirement.get(field)):
                errors.append(f"{label}: mandatory field '{field}' is empty")

        for field in STRING_FIELDS:
            value = requirement.get(field)
            if value is not None and (
                not isinstance(value, str) or not value.strip()
            ):
                errors.append(f"{label}: '{field}' must be a non-empty string")

        statement = requirement.get("statement")
        if isinstance(statement, str) and not re.search(
            r"\bshall\b", statement, re.IGNORECASE
        ):
            errors.append(f"{label}: statement must contain 'shall'")

        for field in LIST_FIELDS:
            value = requirement.get(field)
            if value is not None and (
                not isinstance(value, list)
                or any(not isinstance(item, str) or not item.strip() for item in value)
            ):
                errors.append(f"{label}: '{field}' must be a non-empty string array")

        roles = requirement.get("roles")
        if isinstance(roles, list):
            for role in roles:
                if isinstance(role, str) and role.strip() and role not in ALLOWED_ROLES:
                    choices = ", ".join(sorted(ALLOWED_ROLES))
                    errors.append(
                        f"{label}: invalid role '{role}'; allowed: {choices}"
                    )

        for field, allowed in ALLOWED_VALUES.items():
            value = requirement.get(field)
            if value is not None and value not in allowed:
                choices = ", ".join(sorted(allowed))
                errors.append(f"{label}: invalid {field} '{value}'; allowed: {choices}")

        if requirement.get("status") == "Implemented":
            sources = requirement.get("source")
            if isinstance(sources, list):
                for source in sources:
                    if not isinstance(source, str) or not source.strip():
                        continue
                    source_path = repository_root / source
                    if not source_path.exists():
                        errors.append(
                            f"{label}: implemented source path does not exist: {source}"
                        )

    return errors


def main() -> int:
    if len(sys.argv) != 2:
        print("Usage: validate_srs.py <requirements.json>", file=sys.stderr)
        return 2

    catalog_path = Path(sys.argv[1]).resolve()
    repository_root = Path(__file__).resolve().parents[2]
    errors = validate_catalog(catalog_path, repository_root)
    if errors:
        print(f"FAIL: requirement catalog has {len(errors)} error(s)")
        for error in errors:
            print(f"- {error}")
        return 1

    print("PASS: requirement catalog is valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
