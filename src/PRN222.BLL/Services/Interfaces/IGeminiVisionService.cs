namespace PRN222.BLL.Services.Interfaces;

/// <summary>
/// Service for multimodal AI operations using Gemini Vision.
/// </summary>
public interface IGeminiVisionService
{
    /// <summary>
    /// Describes the content of an image based on a visual prompt.
    /// </summary>
    /// <param name="imageBytes">Raw image data.</param>
    /// <param name="contextHint">Optional context to guide the AI (e.g. "This is a slide from a .NET course").</param>
    /// <returns>A detailed textual description of the image content.</returns>
    Task<string> DescribeImageAsync(byte[] imageBytes, string contextHint);
}
