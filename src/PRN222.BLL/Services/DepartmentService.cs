using Microsoft.EntityFrameworkCore;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.BLL.Services;

public class DepartmentService : IDepartmentService
{
    private readonly IRepository<Department> _departmentRepository;
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly ICourseAccessService _courseAccessService;
    private readonly IUnitOfWork _unitOfWork;

    public DepartmentService(
        IRepository<Department> departmentRepository,
        IRepository<Course> courseRepository,
        IRepository<ApplicationUser> userRepository,
        ICourseAccessService courseAccessService,
        IUnitOfWork unitOfWork)
    {
        _departmentRepository = departmentRepository;
        _courseRepository = courseRepository;
        _userRepository = userRepository;
        _courseAccessService = courseAccessService;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<DepartmentDto>> GetAllAsync()
    {
        return await _departmentRepository.GetQueryable()
            .AsNoTracking()
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                Description = d.Description,
                UserCount = d.Users.Count,
                CourseCount = d.Courses.Count,
                HeadLecturerUserId = d.HeadLecturerUserId,
                HeadLecturerEmail = d.HeadLecturer != null ? d.HeadLecturer.Email : null
            })
            .ToListAsync();
    }

    public async Task<DepartmentDto?> GetByIdAsync(int id)
    {
        return await _departmentRepository.GetQueryable()
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                Description = d.Description,
                UserCount = d.Users.Count,
                CourseCount = d.Courses.Count,
                HeadLecturerUserId = d.HeadLecturerUserId,
                HeadLecturerEmail = d.HeadLecturer != null ? d.HeadLecturer.Email : null
            })
            .FirstOrDefaultAsync();
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto)
    {
        var exists = await _departmentRepository.GetQueryable()
            .AnyAsync(d => d.Code.ToLower() == dto.Code.Trim().ToLower());
        if (exists)
        {
            throw new InvalidOperationException($"Mã khoa '{dto.Code.Trim()}' đã tồn tại.");
        }

        var department = new Department
        {
            Name = dto.Name.Trim(),
            Code = dto.Code.Trim(),
            Description = dto.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _departmentRepository.AddAsync(department);
        await _unitOfWork.SaveChangesAsync();

        return new DepartmentDto
        {
            Id = department.Id,
            Name = department.Name,
            Code = department.Code,
            Description = department.Description,
            UserCount = 0,
            CourseCount = 0,
            HeadLecturerUserId = department.HeadLecturerUserId
        };
    }

    public async Task<int> AssignUserToDepartmentAsync(string userId, int? departmentId)
    {
        var user = await _userRepository.GetQueryable()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new InvalidOperationException($"User '{userId}' not found.");

        await EnsureDepartmentExistsAsync(departmentId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            user.DepartmentId = departmentId;
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync();

            var revoked = await _courseAccessService.ReconcileUserAssignmentsAsync(userId);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();
            return revoked;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<int> AssignCourseToDepartmentAsync(int courseId, int? departmentId)
    {
        var course = await _courseRepository.GetByIdAsync(courseId);
        if (course == null)
            throw new InvalidOperationException($"Course {courseId} not found.");

        await EnsureDepartmentExistsAsync(departmentId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            course.DepartmentId = departmentId;
            _courseRepository.Update(course);
            await _unitOfWork.SaveChangesAsync();

            var revoked = await _courseAccessService.ReconcileCourseAssignmentsAsync(courseId);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();
            return revoked;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task SetHeadLecturerOwnerAsync(int departmentId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var department = await _departmentRepository.GetQueryable()
            .FirstOrDefaultAsync(d => d.Id == departmentId)
            ?? throw new InvalidOperationException($"Department {departmentId} not found.");

        if (!string.IsNullOrWhiteSpace(department.HeadLecturerUserId) &&
            !string.Equals(department.HeadLecturerUserId, userId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Khoa nay da co truong bo mon.");
        }

        var previouslyOwnedDepartments = await _departmentRepository.GetQueryable()
            .Where(d => d.HeadLecturerUserId == userId && d.Id != departmentId)
            .ToListAsync();
        foreach (var ownedDepartment in previouslyOwnedDepartments)
        {
            ownedDepartment.HeadLecturerUserId = null;
            _departmentRepository.Update(ownedDepartment);
        }

        department.HeadLecturerUserId = userId;
        _departmentRepository.Update(department);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ClearHeadLecturerOwnerAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var ownedDepartments = await _departmentRepository.GetQueryable()
            .Where(d => d.HeadLecturerUserId == userId)
            .ToListAsync();

        if (ownedDepartments.Count == 0)
        {
            return;
        }

        foreach (var department in ownedDepartments)
        {
            department.HeadLecturerUserId = null;
            _departmentRepository.Update(department);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<List<ApplicationUserDto>> GetUsersInDepartmentAsync(int departmentId)
    {
        var users = await _userRepository.GetQueryable()
            .AsNoTracking()
            .Where(u => u.DepartmentId == departmentId)
            .ToListAsync();

        return users.Select(u => new ApplicationUserDto
        {
            Id = u.Id,
            Email = u.Email ?? string.Empty,
            UserName = u.UserName ?? string.Empty,
            DepartmentId = u.DepartmentId
        }).ToList();
    }

    public async Task<List<CourseDto>> GetCoursesInDepartmentAsync(int departmentId)
    {
        var courses = await _courseRepository.GetQueryable()
            .AsNoTracking()
            .Where(c => c.DepartmentId == departmentId)
            .ToListAsync();

        return courses.Select(c => new CourseDto(
            c.Id,
            c.Name,
            c.Description,
            0,
            c.CreatedAt
        )).ToList();
    }

    private async Task EnsureDepartmentExistsAsync(int? departmentId)
    {
        if (departmentId is null)
        {
            return;
        }

        var exists = await _departmentRepository.GetQueryable()
            .AsNoTracking()
            .AnyAsync(department => department.Id == departmentId.Value);

        if (!exists)
        {
            throw new InvalidOperationException($"Department {departmentId.Value} not found.");
        }
    }
}
