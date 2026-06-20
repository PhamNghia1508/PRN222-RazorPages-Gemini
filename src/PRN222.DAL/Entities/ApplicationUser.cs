using Microsoft.AspNetCore.Identity;

namespace PRN222.DAL.Entities;

public class ApplicationUser : IdentityUser
{
    public ICollection<ApplicationUserCourse> CourseAssignments { get; set; } = new List<ApplicationUserCourse>();

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
}
