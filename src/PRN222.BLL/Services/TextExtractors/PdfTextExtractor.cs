using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Xobject;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.TextExtractors;

/// <summary>
/// Extracts text and images from PDF files using iText7.
/// </summary>
public class PdfTextExtractor : ITextExtractorService
{
    private static readonly string[] PdfContentTypes =
    {
        "application/pdf"
    };

    public bool CanHandle(string contentType)
    {
        return PdfContentTypes.Contains(contentType.ToLowerInvariant());
    }

    public Task<ExtractedContentDto> ExtractTextAsync(Stream fileStream, string contentType)
    {
        var result = new ExtractedContentDto();
        var textBuilder = new StringBuilder();

        using var pdfReader = new PdfReader(fileStream);
        using var pdfDocument = new PdfDocument(pdfReader);

        for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
        {
            var page = pdfDocument.GetPage(i);

            // Extract text
            var strategy = new SimpleTextExtractionStrategy();
            var text = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page, strategy);

            if (!string.IsNullOrWhiteSpace(text))
            {
                textBuilder.AppendLine($"--- Page {i} ---");
                textBuilder.AppendLine(text);
                textBuilder.AppendLine();
            }

            // Extract images (simplified approach for this project)
            var resources = page.GetResources();
            var xObjects = resources.GetResource(PdfName.XObject);
            if (xObjects != null)
            {
                foreach (var name in xObjects.KeySet())
                {
                    var obj = xObjects.GetAsDictionary(name);
                    if (obj != null && PdfName.Image.Equals(obj.GetAsName(PdfName.Subtype)))
                    {
                        var stream = xObjects.GetAsStream(name);
                        var image = new PdfImageXObject(stream);
                        var imageBytes = image.GetImageBytes();

                        if (imageBytes != null && imageBytes.Length > 10000) // Ignore small icons/noise
                        {
                            result.Images.Add(new ExtractedImageDto
                            {
                                Data = imageBytes,
                                MimeType = "image/png", // iText often returns PNG compatible bytes or we can detect
                                PageNumber = i
                            });
                        }
                    }
                }
            }
        }

        result.Text = textBuilder.ToString().Trim();
        return Task.FromResult(result);
    }
}
