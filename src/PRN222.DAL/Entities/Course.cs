namespace PRN222.DAL.Entities;

/// <summary>
/// Represents a course/subject that contains uploaded documents.
/// </summary>
public class Course
{
    public int Id { get; set; }

    /// <summary>Course name, e.g. "PRN222 - .NET Development"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional course description.</summary>
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Navigation properties
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<ApplicationUserCourse> UserAssignments { get; set; } = new List<ApplicationUserCourse>();
}
