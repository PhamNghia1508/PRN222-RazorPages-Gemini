using PRN222.BLL.DTOs;

namespace PRN222.BLL.Services.Interfaces;

/// <summary>
/// Service interface for course CRUD operations.
/// </summary>
public interface ICourseService
{
    Task<IEnumerable<CourseDto>> GetAllCoursesAsync();
    Task<CourseDto?> GetCourseByIdAsync(int id);
    Task<CourseDto> CreateCourseAsync(string name, string? description, int departmentId);
    Task<CourseDto> UpdateCourseAsync(int id, string name, string? description, int departmentId);
    Task<bool> DeleteCourseAsync(int id);
}
