namespace PRN222.DAL.Entities;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // e.g. CNTT, KT, QTKD
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? HeadLecturerUserId { get; set; }
    public ApplicationUser? HeadLecturer { get; set; }

    // Navigation
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
