namespace PRN222.DAL.Entities;

/// <summary>
/// Represents a citation linking a chat message answer to a specific document chunk.
/// </summary>
public class ChatCitation
{
    public int Id { get; set; }

    public int MessageId { get; set; }

    public int ChunkId { get; set; }

    /// <summary>Relevance score of this chunk to the question (0.0 to 1.0).</summary>
    public float RelevanceScore { get; set; }

    /// <summary>Short excerpt from the chunk used in the answer.</summary>
    public string? SnippetText { get; set; }

    // Navigation properties
    public ChatMessage Message { get; set; } = null!;
    public DocumentChunk Chunk { get; set; } = null!;
}
