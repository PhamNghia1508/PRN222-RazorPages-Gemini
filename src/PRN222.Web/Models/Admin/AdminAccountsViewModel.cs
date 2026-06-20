using System.ComponentModel.DataAnnotations;
using PRN222.BLL.DTOs;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Models.Admin;

public sealed class AdminAccountsViewModel
{
    public CreateStaffAccountViewModel NewAccount { get; init; } = new();

    public IReadOnlyList<StaffAccountListItemViewModel> Accounts { get; init; } = [];

    public IReadOnlyList<CourseDto> Courses { get; init; } = [];

    public IReadOnlyList<DepartmentDto> Departments { get; init; } = [];
}

public sealed class CreateStaffAccountViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [Display(Name = "Vai trò")]
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn Khoa.")]
    [Display(Name = "Khoa")]
    public int? DepartmentId { get; set; }

    public List<int> CourseIds { get; set; } = [];
}

public sealed class StaffAccountListItemViewModel
{
    public required string UserId { get; init; }

    public required string Email { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public IReadOnlyList<int> AssignedCourseIds { get; init; } = [];

    public IReadOnlyList<string> AssignedCourseNames { get; init; } = [];

    public int? DepartmentId { get; init; }

    public string? DepartmentName { get; init; }

    public int InvalidAssignmentCount { get; init; }

    public bool IsLecturer => Roles.Contains(ApplicationRoles.Lecturer);

    public bool IsHeadLecturer => Roles.Contains(ApplicationRoles.HeadLecturer);

    public bool CanAssignCourses => Roles.Any(ApplicationRoles.CanBeAssignedCourses);

    public bool HasValidConfiguration =>
        !CanAssignCourses ||
        (DepartmentId.HasValue &&
         AssignedCourseIds.Count > 0 &&
         InvalidAssignmentCount == 0);

    public string AssignmentLabel => IsHeadLecturer ? "Môn phụ trách" : "Môn được xem";

    public string AssignmentActionLabel => IsHeadLecturer ? "Lưu môn phụ trách" : "Lưu môn được xem";
}
