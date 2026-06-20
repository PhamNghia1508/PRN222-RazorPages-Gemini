namespace PRN222.DAL.Entities;

/// <summary>
/// Represents a chat conversation session with the chatbot.
/// </summary>
public class ChatSession
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>Title of the chat session, auto-generated or user-defined.</summary>
    public string SessionTitle { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastMessageAt { get; set; }

    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
