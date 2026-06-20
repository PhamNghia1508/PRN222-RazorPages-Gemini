using System.Text;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.TextExtractors;

/// <summary>
/// Best-effort text extraction from legacy PPT binaries.
/// </summary>
public class PptTextExtractor : ITextExtractorService
{
    private static readonly string[] PptContentTypes =
    {
        "application/vnd.ms-powerpoint"
    };

    public bool CanHandle(string contentType)
    {
        return PptContentTypes.Contains(contentType.ToLowerInvariant());
    }

    public Task<ExtractedContentDto> ExtractTextAsync(Stream fileStream, string contentType)
    {
        using var buffer = new MemoryStream();
        fileStream.CopyTo(buffer);
        var bytes = buffer.ToArray();

        var candidates = new List<string>();
        candidates.AddRange(ExtractReadableLines(Encoding.Unicode.GetString(bytes)));
        candidates.AddRange(ExtractReadableLines(Encoding.UTF8.GetString(bytes)));

        var uniqueLines = candidates
            .Select(line => line.Trim())
            .Where(line => line.Length >= 8)
            .Distinct()
            .ToList();

        return Task.FromResult(new ExtractedContentDto
        {
            Text = string.Join(Environment.NewLine, uniqueLines)
        });
    }

    private static IEnumerable<string> ExtractReadableLines(string input)
    {
        var sb = new StringBuilder();
        int letterCount = 0;

        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c is '.' or ',' or ':' or ';' or '-' or '_' or '/' or '(' or ')')
            {
                sb.Append(c);
                if (char.IsLetter(c))
                    letterCount++;
                continue;
            }

            if (sb.Length >= 8 && letterCount >= 3)
                yield return sb.ToString();

            sb.Clear();
            letterCount = 0;
        }

        if (sb.Length >= 8 && letterCount >= 3)
            yield return sb.ToString();
    }
}
