using PRN222.BLL.DTOs;

namespace PRN222.BLL.Services.Interfaces;

/// <summary>
/// Service interface for splitting text into chunks.
/// </summary>
public interface IChunkingService
{
    /// <summary>Split text into chunks with the specified size and overlap.</summary>
    IEnumerable<ChunkDto> ChunkText(string text, int chunkSize = 512, int overlap = 50);
}
