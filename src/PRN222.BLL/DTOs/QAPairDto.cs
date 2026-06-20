using System;

namespace PRN222.BLL.DTOs;

public class QAPairDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int? DocumentChunkId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
    public DateTime CreatedAt { get; set; }
}
