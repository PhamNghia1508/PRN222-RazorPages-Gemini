using PRN222.BLL.DTOs;

namespace PRN222.BLL.Services.Interfaces;

/// <summary>
/// Service interface for extracting text and images from documents.
/// </summary>
public interface ITextExtractorService
{
    /// <summary>Extract text and image content from a file stream.</summary>
    Task<ExtractedContentDto> ExtractTextAsync(Stream fileStream, string contentType);

    /// <summary>Check if this extractor can handle the given content type.</summary>
    bool CanHandle(string contentType);
}
