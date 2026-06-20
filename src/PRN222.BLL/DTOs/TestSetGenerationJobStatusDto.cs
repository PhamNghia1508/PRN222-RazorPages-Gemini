namespace PRN222.BLL.DTOs;

public class TestSetGenerationJobStatusDto
{
    public int CourseId { get; set; }
    public string Status { get; set; } = "Idle";
    public int ProcessedChunks { get; set; }
    public int TotalChunks { get; set; }
    public int CreatedQAPairs { get; set; }
    public string? Message { get; set; }
    public string? LastError { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsRunning => Status is "Running" or "Stopping";
}
