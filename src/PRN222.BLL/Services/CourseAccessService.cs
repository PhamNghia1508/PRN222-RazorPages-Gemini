using Microsoft.EntityFrameworkCore;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.BLL.Services;

public class CourseAccessService : ICourseAccessService
{
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<ApplicationUserCourse> _assignmentRepository;

    public CourseAccessService(
        IRepository<ApplicationUser> userRepository,
        IRepository<Course> courseRepository,
        IRepository<ApplicationUserCourse> assignmentRepository)
    {
        _userRepository = userRepository;
        _courseRepository = courseRepository;
        _assignmentRepository = assignmentRepository;
    }

    public async Task<IReadOnlySet<int>> GetAccessibleStaffCourseIdsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new HashSet<int>();
        }

        var courseIds = await (
            from assignment in _assignmentRepository.GetQueryable().AsNoTracking()
            join user in _userRepository.GetQueryable().AsNoTracking()
                on assignment.UserId equals user.Id
            join course in _courseRepository.GetQueryable().AsNoTracking()
                on assignment.CourseId equals course.Id
            where assignment.UserId == userId
                && user.DepartmentId != null
                && course.DepartmentId != null
                && user.DepartmentId == course.DepartmentId
            select course.Id)
            .ToListAsync();

        return courseIds.ToHashSet();
    }

    public async Task<bool> CanStaffAccessCourseAsync(string userId, int courseId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await (
            from assignment in _assignmentRepository.GetQueryable().AsNoTracking()
            join user in _userRepository.GetQueryable().AsNoTracking()
                on assignment.UserId equals user.Id
            join course in _courseRepository.GetQueryable().AsNoTracking()
                on assignment.CourseId equals course.Id
            where assignment.UserId == userId
                && assignment.CourseId == courseId
                && user.DepartmentId != null
                && course.DepartmentId != null
                && user.DepartmentId == course.DepartmentId
            select assignment)
            .AnyAsync();
    }

    public async Task<IReadOnlyList<int>> ValidateDepartmentCourseIdsAsync(
        int departmentId,
        IEnumerable<int> courseIds)
    {
        var requestedCourseIds = (courseIds ?? [])
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (requestedCourseIds.Count == 0)
        {
            throw new InvalidOperationException("Vui lòng chọn ít nhất một môn học.");
        }

        var validCourseIds = await _courseRepository.GetQueryable()
            .AsNoTracking()
            .Where(course => requestedCourseIds.Contains(course.Id) && course.DepartmentId == departmentId)
            .Select(course => course.Id)
            .OrderBy(id => id)
            .ToListAsync();

        if (validCourseIds.Count != requestedCourseIds.Count)
        {
            throw new InvalidOperationException("Một hoặc nhiều môn học không tồn tại hoặc không thuộc khoa.");
        }

        return validCourseIds;
    }

    public async Task<int> ReconcileUserAssignmentsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        var assignments = await _assignmentRepository.GetQueryable()
            .Where(assignment => assignment.UserId == userId)
            .ToListAsync();

        var userDepartmentId = await _userRepository.GetQueryable()
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.DepartmentId)
            .FirstOrDefaultAsync();

        var validCourseIds = userDepartmentId == null
            ? new HashSet<int>()
            : (await _courseRepository.GetQueryable()
                .AsNoTracking()
                .Where(course => course.DepartmentId == userDepartmentId)
                .Select(course => course.Id)
                .ToListAsync())
                .ToHashSet();

        return DeleteInvalidAssignments(assignments, assignment => validCourseIds.Contains(assignment.CourseId));
    }

    public async Task<int> ReconcileCourseAssignmentsAsync(int courseId)
    {
        var assignments = await _assignmentRepository.GetQueryable()
            .Where(assignment => assignment.CourseId == courseId)
            .ToListAsync();

        var courseDepartmentId = await _courseRepository.GetQueryable()
            .AsNoTracking()
            .Where(course => course.Id == courseId)
            .Select(course => course.DepartmentId)
            .FirstOrDefaultAsync();

        var validUserIds = courseDepartmentId == null
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await _userRepository.GetQueryable()
                .AsNoTracking()
                .Where(user => user.DepartmentId == courseDepartmentId)
                .Select(user => user.Id)
                .ToListAsync())
                .ToHashSet(StringComparer.Ordinal);

        return DeleteInvalidAssignments(assignments, assignment => validUserIds.Contains(assignment.UserId));
    }

    private int DeleteInvalidAssignments(
        IEnumerable<ApplicationUserCourse> assignments,
        Func<ApplicationUserCourse, bool> isValid)
    {
        var deletionCount = 0;

        foreach (var assignment in assignments.Where(assignment => !isValid(assignment)))
        {
            _assignmentRepository.Delete(assignment);
            deletionCount++;
        }

        return deletionCount;
    }
}
