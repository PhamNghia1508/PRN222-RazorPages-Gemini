using FluentAssertions;
using Moq;
using PRN222.BLL.Services;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.Tests.Services;

public class CourseAccessServiceTests
{
    private readonly Mock<IRepository<ApplicationUser>> _userRepositoryMock = new();
    private readonly Mock<IRepository<Course>> _courseRepositoryMock = new();
    private readonly Mock<IRepository<ApplicationUserCourse>> _assignmentRepositoryMock = new();

    [Fact]
    public async Task GetAccessibleStaffCourseIdsAsync_ShouldReturnOnlyExplicitAssignmentsInUsersDepartment()
    {
        SetUsers(new ApplicationUser { Id = "lecturer-1", DepartmentId = 10 });
        SetCourses(
            new Course { Id = 1, DepartmentId = 10 },
            new Course { Id = 2, DepartmentId = 20 },
            new Course { Id = 3, DepartmentId = 10 });
        SetAssignments(
            new ApplicationUserCourse { UserId = "lecturer-1", CourseId = 1 },
            new ApplicationUserCourse { UserId = "lecturer-1", CourseId = 2 },
            new ApplicationUserCourse { UserId = "other-user", CourseId = 3 });

        var result = await CreateService().GetAccessibleStaffCourseIdsAsync("lecturer-1");

        result.Should().BeEquivalentTo([1]);
    }

    [Fact]
    public async Task CanStaffAccessCourseAsync_ShouldReturnTrue_WhenDepartmentsMatchAndAssignmentExists()
    {
        SetUsers(new ApplicationUser { Id = "lecturer-1", DepartmentId = 10 });
        SetCourses(new Course { Id = 1, DepartmentId = 10 });
        SetAssignments(new ApplicationUserCourse { UserId = "lecturer-1", CourseId = 1 });

        var result = await CreateService().CanStaffAccessCourseAsync("lecturer-1", 1);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanStaffAccessCourseAsync_ShouldReturnFalse_WhenDepartmentsMatchButAssignmentIsMissing()
    {
        SetUsers(new ApplicationUser { Id = "lecturer-1", DepartmentId = 10 });
        SetCourses(new Course { Id = 1, DepartmentId = 10 });
        SetAssignments();

        var result = await CreateService().CanStaffAccessCourseAsync("lecturer-1", 1);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(10, null)]
    [InlineData(10, 20)]
    public async Task CanStaffAccessCourseAsync_ShouldReturnFalse_WhenDepartmentsAreNullOrMismatched(
        int? userDepartmentId,
        int? courseDepartmentId)
    {
        SetUsers(new ApplicationUser { Id = "lecturer-1", DepartmentId = userDepartmentId });
        SetCourses(new Course { Id = 1, DepartmentId = courseDepartmentId });
        SetAssignments(new ApplicationUserCourse { UserId = "lecturer-1", CourseId = 1 });

        var result = await CreateService().CanStaffAccessCourseAsync("lecturer-1", 1);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StaffAccessMethods_ShouldReturnEmptyOrFalse_WhenUserIdIsEmpty(string userId)
    {
        var service = CreateService();

        var courseIds = await service.GetAccessibleStaffCourseIdsAsync(userId);
        var canAccess = await service.CanStaffAccessCourseAsync(userId, 1);

        courseIds.Should().BeEmpty();
        canAccess.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDepartmentCourseIdsAsync_ShouldRejectEmptyNormalizedIds()
    {
        SetCourses();

        var act = () => CreateService().ValidateDepartmentCourseIdsAsync(10, [0, -1, 0]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ít nhất một môn học*");
    }

    [Fact]
    public async Task ValidateDepartmentCourseIdsAsync_ShouldRejectNullCourseIdsAsEmptySelection()
    {
        SetCourses();

        var act = () => CreateService().ValidateDepartmentCourseIdsAsync(10, null!);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ít nhất một môn học*");
    }

    [Theory]
    [InlineData(99)]
    [InlineData(2)]
    public async Task ValidateDepartmentCourseIdsAsync_ShouldRejectMissingOrCrossDepartmentCourse(int invalidCourseId)
    {
        SetCourses(
            new Course { Id = 1, DepartmentId = 10 },
            new Course { Id = 2, DepartmentId = 20 });

        var act = () => CreateService().ValidateDepartmentCourseIdsAsync(10, [1, invalidCourseId]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*môn học không tồn tại hoặc không thuộc khoa*");
    }

    [Fact]
    public async Task ValidateDepartmentCourseIdsAsync_ShouldReturnPositiveDistinctValidIds()
    {
        SetCourses(
            new Course { Id = 1, DepartmentId = 10 },
            new Course { Id = 2, DepartmentId = 10 });

        var result = await CreateService().ValidateDepartmentCourseIdsAsync(10, [2, 0, 1, 2, -1]);

        result.Should().Equal(1, 2);
    }

    [Fact]
    public async Task ReconcileUserAssignmentsAsync_ShouldDeleteAssignmentsOutsideUsersDepartment()
    {
        var validAssignment = new ApplicationUserCourse { UserId = "lecturer-1", CourseId = 1 };
        var invalidAssignment = new ApplicationUserCourse { UserId = "lecturer-1", CourseId = 2 };
        SetUsers(new ApplicationUser { Id = "lecturer-1", DepartmentId = 10 });
        SetCourses(
            new Course { Id = 1, DepartmentId = 10 },
            new Course { Id = 2, DepartmentId = 20 });
        SetAssignments(validAssignment, invalidAssignment);

        var result = await CreateService().ReconcileUserAssignmentsAsync("lecturer-1");

        result.Should().Be(1);
        _assignmentRepositoryMock.Verify(r => r.Delete(invalidAssignment), Times.Once);
        _assignmentRepositoryMock.Verify(r => r.Delete(validAssignment), Times.Never);
    }

    [Fact]
    public async Task ReconcileCourseAssignmentsAsync_ShouldDeleteAssignmentsForUsersOutsideCoursesDepartment()
    {
        var validAssignment = new ApplicationUserCourse { UserId = "lecturer-1", CourseId = 1 };
        var invalidAssignment = new ApplicationUserCourse { UserId = "lecturer-2", CourseId = 1 };
        SetUsers(
            new ApplicationUser { Id = "lecturer-1", DepartmentId = 10 },
            new ApplicationUser { Id = "lecturer-2", DepartmentId = 20 });
        SetCourses(new Course { Id = 1, DepartmentId = 10 });
        SetAssignments(validAssignment, invalidAssignment);

        var result = await CreateService().ReconcileCourseAssignmentsAsync(1);

        result.Should().Be(1);
        _assignmentRepositoryMock.Verify(r => r.Delete(invalidAssignment), Times.Once);
        _assignmentRepositoryMock.Verify(r => r.Delete(validAssignment), Times.Never);
    }

    private CourseAccessService CreateService()
    {
        return new CourseAccessService(
            _userRepositoryMock.Object,
            _courseRepositoryMock.Object,
            _assignmentRepositoryMock.Object);
    }

    private void SetUsers(params ApplicationUser[] users)
    {
        _userRepositoryMock.Setup(r => r.GetQueryable()).Returns(users.AsAsyncQueryable());
    }

    private void SetCourses(params Course[] courses)
    {
        _courseRepositoryMock.Setup(r => r.GetQueryable()).Returns(courses.AsAsyncQueryable());
    }

    private void SetAssignments(params ApplicationUserCourse[] assignments)
    {
        _assignmentRepositoryMock.Setup(r => r.GetQueryable()).Returns(assignments.AsAsyncQueryable());
    }
}
