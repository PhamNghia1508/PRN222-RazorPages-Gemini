using Microsoft.EntityFrameworkCore;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.BLL.Services;

public class CourseAssignmentService : ICourseAssignmentService
{
    private readonly IRepository<ApplicationUserCourse> _assignmentRepository;
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly ICourseAccessService _courseAccessService;
    private readonly IUnitOfWork _unitOfWork;

    public CourseAssignmentService(
        IRepository<ApplicationUserCourse> assignmentRepository,
        IRepository<ApplicationUser> userRepository,
        ICourseAccessService courseAccessService,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _userRepository = userRepository;
        _courseAccessService = courseAccessService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlySet<int>> GetAssignedCourseIdsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new HashSet<int>();
        }

        var courseIds = await _assignmentRepository.GetQueryable()
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId)
            .Select(assignment => assignment.CourseId)
            .ToListAsync();

        return courseIds.ToHashSet();
    }

    public async Task<IReadOnlyList<string>> GetAssignedUserIdsForCourseAsync(int courseId)
    {
        if (courseId <= 0)
        {
            return [];
        }

        return await _assignmentRepository.GetQueryable()
            .AsNoTracking()
            .Where(assignment => assignment.CourseId == courseId)
            .Select(assignment => assignment.UserId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<int>>> GetAssignedCourseIdsByUserAsync(IEnumerable<string> userIds)
    {
        var userIdSet = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (userIdSet.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<int>>();
        }

        var assignments = await _assignmentRepository.GetQueryable()
            .AsNoTracking()
            .Where(assignment => userIdSet.Contains(assignment.UserId))
            .Select(assignment => new { assignment.UserId, assignment.CourseId })
            .ToListAsync();

        return assignments
            .GroupBy(assignment => assignment.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(assignment => assignment.CourseId).OrderBy(id => id).ToList(),
                StringComparer.Ordinal);
    }

    public async Task ReplaceStaffAssignmentsAsync(string userId, int departmentId, IEnumerable<int> courseIds)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var user = await _userRepository.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");

        if (user.DepartmentId != departmentId)
        {
            throw new InvalidOperationException("Tài khoản phải thuộc đúng Khoa.");
        }

        var validCourseIds = await _courseAccessService.ValidateDepartmentCourseIdsAsync(departmentId, courseIds);
        var validCourseIdSet = validCourseIds.ToHashSet();
        var existingAssignments = await _assignmentRepository.GetQueryable()
            .Where(assignment => assignment.UserId == userId)
            .ToListAsync();

        foreach (var assignment in existingAssignments.Where(assignment => !validCourseIdSet.Contains(assignment.CourseId)))
        {
            _assignmentRepository.Delete(assignment);
        }

        var existingCourseIds = existingAssignments
            .Where(assignment => validCourseIdSet.Contains(assignment.CourseId))
            .Select(assignment => assignment.CourseId)
            .ToHashSet();

        foreach (var courseId in validCourseIdSet.Except(existingCourseIds))
        {
            await _assignmentRepository.AddAsync(new ApplicationUserCourse
            {
                UserId = userId,
                CourseId = courseId,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }
}
