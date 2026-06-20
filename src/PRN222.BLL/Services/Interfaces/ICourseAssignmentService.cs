namespace PRN222.BLL.Services.Interfaces;

public interface ICourseAssignmentService
{
    Task<IReadOnlySet<int>> GetAssignedCourseIdsAsync(string userId);

    Task<IReadOnlyList<string>> GetAssignedUserIdsForCourseAsync(int courseId);

    Task<IReadOnlyDictionary<string, IReadOnlyList<int>>> GetAssignedCourseIdsByUserAsync(IEnumerable<string> userIds);

    Task ReplaceStaffAssignmentsAsync(string userId, int departmentId, IEnumerable<int> courseIds);
}
