using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PRN222.BLL.Services;

/// <summary>
/// Service for course management operations.
/// </summary>
public class CourseService : ICourseService
{
    private readonly IRepository<Course> _courseRepository;
    private readonly ICourseAccessService _courseAccessService;
    private readonly IUnitOfWork _unitOfWork;

    public CourseService(
        IRepository<Course> courseRepository,
        ICourseAccessService courseAccessService,
        IUnitOfWork unitOfWork)
    {
        _courseRepository = courseRepository;
        _courseAccessService = courseAccessService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<CourseDto>> GetAllCoursesAsync()
    {
        var courses = await _courseRepository.GetQueryable()
            .Include(c => c.Documents)
            .Include(c => c.Department)
            .AsNoTracking()
            .ToListAsync();
        return courses.Select(ToDto);
    }

    public async Task<CourseDashboardSummaryDto> GetDashboardSummaryAsync(IEnumerable<int>? courseIds = null)
    {
        var courseIdList = courseIds?.Distinct().ToList();
        var query = _courseRepository.GetQueryable().AsNoTracking();
        if (courseIdList is not null)
        {
            query = query.Where(course => courseIdList.Contains(course.Id));
        }

        var visibleCourseIds = await query
            .OrderBy(course => course.Id)
            .Select(course => course.Id)
            .ToListAsync();

        return new CourseDashboardSummaryDto(visibleCourseIds.Count, visibleCourseIds);
    }

    public async Task<CourseDto?> GetCourseByIdAsync(int id)
    {
        var course = await _courseRepository.GetQueryable()
            .Include(c => c.Documents)
            .Include(c => c.Department)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (course == null) return null;

        return ToDto(course);
    }

    public async Task<CourseDto> CreateCourseAsync(string name, string? description, int departmentId)
    {
        var course = new Course
        {
            Name = name,
            Description = description,
            DepartmentId = departmentId,
            CreatedAt = DateTime.UtcNow
        };

        await _courseRepository.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();

        return ToDto(course);
    }

    public async Task<CourseDto> UpdateCourseAsync(int id, string name, string? description, int departmentId)
    {
        var course = await _courseRepository.GetByIdAsync(id);
        if (course == null)
        {
            throw new Exception("Môn học không tồn tại.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var departmentChanged = course.DepartmentId != departmentId;
            course.Name = name;
            course.Description = description;
            course.DepartmentId = departmentId;
            if (departmentChanged)
            {
                course.Department = null;
            }

            _courseRepository.Update(course);
            await _unitOfWork.SaveChangesAsync();

            if (departmentChanged)
            {
                await _courseAccessService.ReconcileCourseAssignmentsAsync(course.Id);
                await _unitOfWork.SaveChangesAsync();
            }

            await _unitOfWork.CommitTransactionAsync();
            return ToDto(course);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<bool> DeleteCourseAsync(int id)
    {
        var course = await _courseRepository.GetQueryable()
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (course == null)
        {
            return false;
        }

        if (course.Documents != null && course.Documents.Any())
        {
            throw new InvalidOperationException("Không thể xóa môn học đã có tài liệu tải lên.");
        }

        _courseRepository.Delete(course);
        await _unitOfWork.SaveChangesAsync();
        
        return true;
    }

    private static CourseDto ToDto(Course course)
    {
        return new CourseDto(
            course.Id,
            course.Name,
            course.Description,
            course.Documents?.Count ?? 0,
            course.CreatedAt)
        {
            DepartmentId = course.DepartmentId,
            DepartmentName = course.Department?.Name
        };
    }
}
