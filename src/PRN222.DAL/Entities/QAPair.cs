using System;

namespace PRN222.DAL.Entities;

/// <summary>
/// Represents a Question-Answer pair generated for Finetuning.
/// </summary>
public class QAPair
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    
    /// <summary>
    /// The source chunk from which this QA was generated (optional)
    /// </summary>
    public int? DocumentChunkId { get; set; }
    
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    
    /// <summary>
    /// LLM-assessed confidence or relevance score for this QA pair.
    /// </summary>
    public double RelevanceScore { get; set; } = 1.0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Course? Course { get; set; }
    public DocumentChunk? DocumentChunk { get; set; }
}
