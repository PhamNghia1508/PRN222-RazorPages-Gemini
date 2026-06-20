using PRN222.BLL.DTOs;

namespace PRN222.Web.Models.Department;

public sealed class DepartmentManagementViewModel
{
    public IReadOnlyList<DepartmentDto> Departments { get; init; } = [];
    public int? SelectedDepartmentId { get; init; }
    public DepartmentDto? SelectedDepartment { get; init; }
    public IReadOnlyList<DepartmentStaffItemViewModel> Staff { get; init; } = [];
    public IReadOnlyList<DepartmentCourseItemViewModel> Courses { get; init; } = [];
}

public sealed class DepartmentStaffItemViewModel
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int AssignedCourseCount { get; init; }
    public int InvalidAssignmentCount { get; init; }
    public bool IsHeadLecturer { get; init; }
    public string RoleLabel => IsHeadLecturer ? "Trưởng bộ môn" : "Giảng viên";
}

public sealed class DepartmentCourseItemViewModel
{
    public int CourseId { get; init; }
    public required string Name { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int AssignedStaffCount { get; init; }
    public int InvalidAssignmentCount { get; init; }
}
