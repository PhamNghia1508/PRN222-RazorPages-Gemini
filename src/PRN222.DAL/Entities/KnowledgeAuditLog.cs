using System;

namespace PRN222.DAL.Entities;

public class KnowledgeAuditLog
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public int? MessageId { get; set; }

    public string TriggeredQuestion { get; set; } = string.Empty;
    public string SuggestedAnswer { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"

    public string UpdatedByUserId { get; set; } = string.Empty;
    public string? ApprovedByUserId { get; set; }
    public string? RejectionReason { get; set; }

    public int? CreatedChunkId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Course Course { get; set; } = null!;
    public ChatMessage? Message { get; set; }
    public ApplicationUser UpdatedByUser { get; set; } = null!;
    public ApplicationUser? ApprovedByUser { get; set; }
    public DocumentChunk? CreatedChunk { get; set; }
}
