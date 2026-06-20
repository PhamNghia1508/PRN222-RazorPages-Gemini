using System.ComponentModel.DataAnnotations;
using PRN222.BLL.DTOs;

namespace PRN222.Web.Models.Course;

public sealed class CourseFormViewModel
{
    public int Id { get; init; }

    [Required(ErrorMessage = "Vui lòng nhập tên môn học.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn Khoa phụ trách.")]
    public int? DepartmentId { get; set; }

    public int DocumentCount { get; init; }

    public IReadOnlyList<DepartmentDto> Departments { get; init; } = [];
}
