namespace PRN222.BLL.Services.Interfaces;

public interface ICourseAccessService
{
    Task<IReadOnlySet<int>> GetAccessibleStaffCourseIdsAsync(string userId);
    Task<bool> CanStaffAccessCourseAsync(string userId, int courseId);
    Task<IReadOnlyList<int>> ValidateDepartmentCourseIdsAsync(int departmentId, IEnumerable<int> courseIds);
    Task<int> ReconcileUserAssignmentsAsync(string userId);
    Task<int> ReconcileCourseAssignmentsAsync(int courseId);
}
