using PRN222.BLL.DTOs;

namespace PRN222.BLL.Services.Interfaces;

public interface IDepartmentService
{
    Task<List<DepartmentDto>> GetAllAsync();
    Task<DepartmentDto?> GetByIdAsync(int id);
    Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto);
    Task<int> AssignUserToDepartmentAsync(string userId, int? departmentId);
    Task<int> AssignCourseToDepartmentAsync(int courseId, int? departmentId);
    Task SetHeadLecturerOwnerAsync(int departmentId, string userId);
    Task ClearHeadLecturerOwnerAsync(string userId);
    Task<List<ApplicationUserDto>> GetUsersInDepartmentAsync(int departmentId);
    Task<List<CourseDto>> GetCoursesInDepartmentAsync(int departmentId);
}
