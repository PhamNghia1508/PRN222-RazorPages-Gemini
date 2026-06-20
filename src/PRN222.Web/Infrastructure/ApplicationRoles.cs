namespace PRN222.Web.Infrastructure;

public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string HeadLecturer = "HeadLecturer";
    public const string Lecturer = "Lecturer";
    public const string Student = "Student";

    public const string Management = "Admin,HeadLecturer,Lecturer";
    public const string DocumentUpload = HeadLecturer;
    public const string ModelOperations = "Admin,HeadLecturer";
    public const string ChatUsers = "Student,Lecturer,HeadLecturer,Admin";

    public static readonly string[] All = [Admin, HeadLecturer, Lecturer, Student];
    public static readonly string[] StaffCreatableByAdmin = [HeadLecturer, Lecturer];
    public static readonly string[] CourseAssignableByAdmin = [HeadLecturer, Lecturer];

    public static bool CanBeAssignedCourses(string role) =>
        CourseAssignableByAdmin.Contains(role);

    public static string GetDisplayName(string role) => role switch
    {
        Admin => "Admin",
        HeadLecturer => "Trưởng bộ môn",
        Lecturer => "Giảng viên",
        Student => "Sinh viên",
        _ => role
    };
}
