namespace PRN222.BLL.DTOs;

/// <summary>
/// DTO for course information.
/// </summary>
public record CourseDto(
    int Id,
    string Name,
    string? Description,
    int DocumentCount,
    DateTime CreatedAt)
{
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
}
