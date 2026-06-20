namespace PRN222.DAL.Entities;

public class ApplicationUserCourse
{
    public string UserId { get; set; } = string.Empty;

    public int CourseId { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;

    public Course Course { get; set; } = null!;
}
