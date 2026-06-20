using System.Text.RegularExpressions;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services;

/// <summary>
/// Service for splitting text into overlapping chunks using fixed-size strategy.
/// Enhanced with Vietnamese word-boundary awareness and page-noise text filters.
/// </summary>
public class ChunkingService : IChunkingService
{
    private static readonly Regex SolitaryNumbersRegex = new Regex(@"^\d+$", RegexOptions.Compiled);
    private static readonly Regex RunningPageHeadersRegex = new Regex(@"^(?i)(trang|page)\s*\d+$", RegexOptions.Compiled);

    /// <summary>
    /// Split text into chunks with the specified size and overlap.
    /// Employs Vietnamese word-boundary awareness and paragraph noise cleaning.
    /// </summary>
    public IEnumerable<ChunkDto> ChunkText(string text, int chunkSize = 512, int overlap = 50)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        // Step 1: Preprocess text to filter out page numbers, headers, footers and isolate line noise
        text = CleanText(text);

        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var chunks = new List<ChunkDto>();
        int position = 0;
        int chunkIndex = 0;

        // Ensure overlap is smaller than chunkSize
        if (overlap >= chunkSize)
        {
            overlap = chunkSize / 10; // Fallback to 10% of chunk size
        }

        while (position < text.Length)
        {
            int end = Math.Min(position + chunkSize, text.Length);

            // Step 2: Try to break at a sentence boundary (., !, ?, newline)
            if (end < text.Length)
            {
                int sentenceEnd = FindSentenceBoundary(text, position + (chunkSize / 2), end);
                if (sentenceEnd > position)
                {
                    end = sentenceEnd;
                }
                else
                {
                    // Word boundary adjustment: Ensure we do not split in the middle of a Vietnamese syllable/word
                    while (end < text.Length && !char.IsWhiteSpace(text[end]))
                    {
                        end++;
                    }
                }
            }

            string chunkContent = text[position..end].Trim();

            if (!string.IsNullOrWhiteSpace(chunkContent))
            {
                int tokenCount = EstimateTokenCount(chunkContent);

                yield return new ChunkDto(
                    Id: 0,
                    ChunkIndex: chunkIndex,
                    Content: chunkContent,
                    TokenCount: tokenCount,
                    StartPage: null,
                    EndPage: null,
                    ChapterSection: null
                );

                chunkIndex++;
            }

            // Move position forward, accounting for overlap
            if (end >= text.Length)
            {
                break; // Reached the end of text, exit loop
            }

            int nextPosition = end - overlap;
            
            // Safety guard & Word boundary adjustment for overlap
            if (nextPosition <= position)
            {
                position = end;
            }
            else
            {
                // Slide overlap start point backwards to start at a full word boundary, limited by the overlap size
                int tempPosition = nextPosition;
                int minPosition = Math.Max(position, nextPosition - overlap);
                while (tempPosition > minPosition && !char.IsWhiteSpace(text[tempPosition]))
                {
                    tempPosition--;
                }

                if (tempPosition > minPosition)
                {
                    position = tempPosition;
                }
                else
                {
                    // If no space was found backwards within the allowed overlap window, slide FORWARDS to the next space
                    // to prevent both infinite loops and word fragmentation.
                    tempPosition = nextPosition;
                    while (tempPosition < text.Length && !char.IsWhiteSpace(text[tempPosition]))
                    {
                        tempPosition++;
                    }

                    if (tempPosition < text.Length)
                    {
                        position = tempPosition; // Lands on space, which will be trimmed in the next chunk
                    }
                    else
                    {
                        position = end; // Fallback to end
                    }
                }
            }
        }
    }

    /// <summary>
    /// Clean up raw extracted text by normalizing line breaks, removing page numbers,
    /// isolated footnote indexes, and redundant whitespaces.
    /// </summary>
    private static string CleanText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return string.Empty;

        // Normalize Unicode to precomposed Form C to avoid fragmented character counting (NFD vs NFC)
        rawText = rawText.Normalize(System.Text.NormalizationForm.FormC);

        // Normalize carriage returns
        rawText = rawText.Replace("\r\n", "\n").Replace("\r", "\n");

        var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            // Filter out solitary numbers (page numbers / footnote reference pointers)
            .Where(line => !SolitaryNumbersRegex.IsMatch(line))
            // Filter out common header/footer page patterns (e.g. "Trang 1", "Page 2")
            .Where(line => !RunningPageHeadersRegex.IsMatch(line))
            .Where(line => line.Length > 0);

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Find the nearest sentence boundary between midPoint and maxEnd.
    /// </summary>
    private static int FindSentenceBoundary(string text, int midPoint, int maxEnd)
    {
        // Look for sentence-ending punctuation followed by whitespace
        for (int i = maxEnd - 1; i >= midPoint; i--)
        {
            char c = text[i];
            if ((c == '.' || c == '!' || c == '?' || c == '\n') &&
                (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1])))
            {
                return i + 1;
            }
        }

        // Fallback: look for any whitespace boundary
        for (int i = maxEnd - 1; i >= midPoint; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i + 1;
            }
        }

        return maxEnd;
    }

    /// <summary>
    /// Rough estimate of token count (~1 token per 4 characters for English,
    /// ~1 token per 2-3 characters for Vietnamese).
    /// </summary>
    private static int EstimateTokenCount(string text)
    {
        int wordCount = text.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)(wordCount * 1.3)); // Vietnamese tends to have more tokens per word
    }
}
