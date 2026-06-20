using FluentAssertions;
using Moq;
using PRN222.BLL.Services;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.Tests.Services;

public class CourseAssignmentServiceTests
{
    private readonly Mock<IRepository<ApplicationUserCourse>> _assignmentRepositoryMock = new();
    private readonly Mock<IRepository<ApplicationUser>> _userRepositoryMock = new();
    private readonly Mock<ICourseAccessService> _courseAccessServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task ReplaceStaffAssignmentsAsync_WhenCourseIsOutsideDepartment_ThrowsWithoutChangingAssignments()
    {
        SetUsers(new ApplicationUser { Id = "staff-1", DepartmentId = 10 });
        SetAssignments(new ApplicationUserCourse { UserId = "staff-1", CourseId = 1 });
        _courseAccessServiceMock
            .Setup(service => service.ValidateDepartmentCourseIdsAsync(10, It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 2 }))))
            .ThrowsAsync(new InvalidOperationException("Tất cả môn được chọn phải thuộc đúng Khoa."));

        var act = () => CreateService().ReplaceStaffAssignmentsAsync("staff-1", 10, [2]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*đúng Khoa*");
        _assignmentRepositoryMock.Verify(repository => repository.Delete(It.IsAny<ApplicationUserCourse>()), Times.Never);
        _assignmentRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<ApplicationUserCourse>()), Times.Never);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ReplaceStaffAssignmentsAsync_WhenCourseIdsAreEmpty_Throws()
    {
        SetUsers(new ApplicationUser { Id = "staff-1", DepartmentId = 10 });
        _courseAccessServiceMock
            .Setup(service => service.ValidateDepartmentCourseIdsAsync(10, It.IsAny<IEnumerable<int>>()))
            .ThrowsAsync(new InvalidOperationException("Vui lòng chọn ít nhất một môn học."));

        var act = () => CreateService().ReplaceStaffAssignmentsAsync("staff-1", 10, []);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ít nhất một môn học*");
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ReplaceStaffAssignmentsAsync_WhenUserDepartmentDoesNotMatch_Throws()
    {
        SetUsers(new ApplicationUser { Id = "staff-1", DepartmentId = 20 });

        var act = () => CreateService().ReplaceStaffAssignmentsAsync("staff-1", 10, [1]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*đúng Khoa*");
        _courseAccessServiceMock.Verify(
            service => service.ValidateDepartmentCourseIdsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<int>>()),
            Times.Never);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ReplaceStaffAssignmentsAsync_WhenAssignmentsAreValid_ReplacesExistingAssignments()
    {
        var removedAssignment = new ApplicationUserCourse { UserId = "staff-1", CourseId = 1 };
        var keptAssignment = new ApplicationUserCourse { UserId = "staff-1", CourseId = 2 };
        SetUsers(new ApplicationUser { Id = "staff-1", DepartmentId = 10 });
        SetAssignments(removedAssignment, keptAssignment);
        _courseAccessServiceMock
            .Setup(service => service.ValidateDepartmentCourseIdsAsync(10, It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync([2, 3]);

        await CreateService().ReplaceStaffAssignmentsAsync("staff-1", 10, [2, 3]);

        _assignmentRepositoryMock.Verify(repository => repository.Delete(removedAssignment), Times.Once);
        _assignmentRepositoryMock.Verify(repository => repository.Delete(keptAssignment), Times.Never);
        _assignmentRepositoryMock.Verify(repository => repository.AddAsync(
            It.Is<ApplicationUserCourse>(assignment =>
                assignment.UserId == "staff-1" &&
                assignment.CourseId == 3)),
            Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(), Times.Once);
    }

    private CourseAssignmentService CreateService()
    {
        return new CourseAssignmentService(
            _assignmentRepositoryMock.Object,
            _userRepositoryMock.Object,
            _courseAccessServiceMock.Object,
            _unitOfWorkMock.Object);
    }

    private void SetUsers(params ApplicationUser[] users)
    {
        _userRepositoryMock.Setup(repository => repository.GetQueryable())
            .Returns(users.AsAsyncQueryable());
    }

    private void SetAssignments(params ApplicationUserCourse[] assignments)
    {
        _assignmentRepositoryMock.Setup(repository => repository.GetQueryable())
            .Returns(assignments.AsAsyncQueryable());
    }
}
