using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.TextExtractors;

/// <summary>
/// Factory for selecting the appropriate text extractor based on content type.
/// Implements the Factory Pattern.
/// </summary>
public class TextExtractorFactory
{
    private readonly IEnumerable<ITextExtractorService> _extractors;

    public TextExtractorFactory(IEnumerable<ITextExtractorService> extractors)
    {
        _extractors = extractors;
    }

    /// <summary>
    /// Get the appropriate text extractor for the given content type.
    /// </summary>
    /// <param name="contentType">MIME type of the file.</param>
    /// <returns>The matching extractor.</returns>
    /// <exception cref="NotSupportedException">Thrown when no extractor supports the content type.</exception>
    public ITextExtractorService GetExtractor(string contentType)
    {
        var extractor = _extractors.FirstOrDefault(e => e.CanHandle(contentType));

        if (extractor == null)
        {
            throw new NotSupportedException(
                $"File type '{contentType}' is not supported. Supported types: PDF, DOCX, PPTX, PPT.");
        }

        return extractor;
    }

    /// <summary>Check if any extractor can handle the given content type.</summary>
    public bool IsSupported(string contentType)
    {
        return _extractors.Any(e => e.CanHandle(contentType));
    }
}
