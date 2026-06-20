using System.Text.Json.Serialization;

namespace PRN222.BLL.DTOs;

/// <summary>
/// DTO for asking a question in the Chat/RAG workflow.
/// Uses JsonPropertyName to ensure correct binding from camelCase JSON payloads.
/// </summary>
public class AskQuestionDto
{
    [JsonPropertyName("courseId")]
    public int CourseId { get; set; }

    [JsonPropertyName("sessionId")]
    public int? SessionId { get; set; }

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonIgnore]
    public string UserId { get; set; } = string.Empty;
}
