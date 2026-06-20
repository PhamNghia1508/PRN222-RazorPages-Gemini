using PRN222.DAL.Entities.Enums;

namespace PRN222.DAL.Entities;

/// <summary>
/// Represents a single message in a chat session (either user question or assistant answer).
/// </summary>
public class ChatMessage
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int? CourseId { get; set; }

    /// <summary>Role of the message sender (User or Assistant).</summary>
    public MessageRole Role { get; set; }

    /// <summary>The message content text.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Confidence score of the assistant's answer (0.0 to 1.0), null for user messages.</summary>
    public float? ConfidenceScore { get; set; }

    /// <summary>Whether the user marked this assistant message as helpful (true), unhelpful (false), or null (unrated).</summary>
    public bool? IsHelpful { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ChatSession Session { get; set; } = null!;
    public Course? Course { get; set; }
    public ICollection<ChatCitation> Citations { get; set; } = new List<ChatCitation>();
}
