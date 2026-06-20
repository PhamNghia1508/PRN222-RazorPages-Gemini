using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.TextExtractors;

/// <summary>
/// Extracts text and images from PPTX files using DocumentFormat.OpenXml.
/// </summary>
public class PptxTextExtractor : ITextExtractorService
{
    private static readonly string[] PptxContentTypes =
    {
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.ms-powerpoint"
    };

    public bool CanHandle(string contentType)
    {
        return PptxContentTypes.Contains(contentType.ToLowerInvariant());
    }

    public Task<ExtractedContentDto> ExtractTextAsync(Stream fileStream, string contentType)
    {
        var result = new ExtractedContentDto();
        var textBuilder = new StringBuilder();

        using var presentation = PresentationDocument.Open(fileStream, false);
        var slideParts = presentation.PresentationPart?.SlideParts?.ToList();
        if (slideParts == null || slideParts.Count == 0)
        {
            return Task.FromResult(result);
        }

        int slideIndex = 1;
        foreach (var slidePart in slideParts)
        {
            var slide = slidePart.Slide;
            if (slide == null)
            {
                slideIndex++;
                continue;
            }

            var texts = slide.Descendants<Text>()
                .Select(t => t.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (texts.Count > 0)
            {
                textBuilder.AppendLine($"--- Slide {slideIndex} ---");
                foreach (var line in texts)
                {
                    textBuilder.AppendLine(line);
                }
                textBuilder.AppendLine();
            }

            // Extract images (Vision-Augmented RAG)
            foreach (var imagePart in slidePart.ImageParts)
            {
                using var stream = imagePart.GetStream();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var imageBytes = ms.ToArray();

                if (imageBytes.Length > 10000) // Ignore small icons/noise
                {
                    result.Images.Add(new ExtractedImageDto
                    {
                        Data = imageBytes,
                        MimeType = imagePart.ContentType,
                        PageNumber = slideIndex
                    });
                }
            }

            slideIndex++;
        }

        result.Text = textBuilder.ToString().Trim();
        return Task.FromResult(result);
    }
}
