using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.TextExtractors;

/// <summary>
/// Extracts text from DOCX files using DocumentFormat.OpenXml.
/// </summary>
public class DocxTextExtractor : ITextExtractorService
{
    private static readonly string[] DocxContentTypes =
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/msword"
    };

    public bool CanHandle(string contentType)
    {
        return DocxContentTypes.Contains(contentType.ToLowerInvariant());
    }

    public Task<ExtractedContentDto> ExtractTextAsync(Stream fileStream, string contentType)
    {
        var textBuilder = new StringBuilder();

        using var document = WordprocessingDocument.Open(fileStream, false);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body != null)
        {
            foreach (var paragraph in body.Elements<Paragraph>())
            {
                var text = paragraph.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    textBuilder.AppendLine(text);
                }
            }
        }

        return Task.FromResult(new ExtractedContentDto
        {
            Text = textBuilder.ToString().Trim()
        });
    }
}
