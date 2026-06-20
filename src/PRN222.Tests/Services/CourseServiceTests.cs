using FluentAssertions;
using Moq;
using PRN222.BLL.Services;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.Tests.Services;

public class CourseServiceTests
{
    [Fact]
    public async Task GetAllCoursesAsync_IncludesDepartmentMetadata()
    {
        var courseRepository = new Mock<IRepository<Course>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        courseRepository.Setup(repository => repository.GetQueryable())
            .Returns(new List<Course>
            {
                new()
                {
                    Id = 1,
                    Name = "PRN222",
                    DepartmentId = 10,
                    Department = new Department { Id = 10, Name = "Công nghệ thông tin" },
                    CreatedAt = new DateTime(2026, 6, 15)
                }
            }.AsAsyncQueryable());
        var service = new CourseService(
            courseRepository.Object,
            new Mock<ICourseAccessService>().Object,
            unitOfWork.Object);

        var result = (await service.GetAllCoursesAsync()).Single();

        result.DepartmentId.Should().Be(10);
        result.DepartmentName.Should().Be("Công nghệ thông tin");
    }

    [Fact]
    public async Task CreateCourseAsync_AssignsSelectedDepartment()
    {
        var courseRepository = new Mock<IRepository<Course>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        Course? createdCourse = null;
        courseRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Course>()))
            .Callback<Course>(course => createdCourse = course)
            .Returns(Task.CompletedTask);
        var service = new CourseService(
            courseRepository.Object,
            new Mock<ICourseAccessService>().Object,
            unitOfWork.Object);

        await service.CreateCourseAsync("PRN222", "ASP.NET Core", 10);

        createdCourse.Should().NotBeNull();
        createdCourse!.DepartmentId.Should().Be(10);
        unitOfWork.Verify(item => item.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateCourseAsync_WhenDepartmentChanges_ReconcilesAssignmentsInTransaction()
    {
        var course = new Course
        {
            Id = 1,
            Name = "PRN222",
            DepartmentId = 10,
            CreatedAt = DateTime.UtcNow
        };
        var courseRepository = new Mock<IRepository<Course>>();
        courseRepository.Setup(repository => repository.GetByIdAsync(course.Id)).ReturnsAsync(course);
        var courseAccessService = new Mock<ICourseAccessService>();
        courseAccessService.Setup(service => service.ReconcileCourseAssignmentsAsync(course.Id)).ReturnsAsync(2);
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new CourseService(courseRepository.Object, courseAccessService.Object, unitOfWork.Object);

        var result = await service.UpdateCourseAsync(course.Id, "PRN222 updated", "Updated", 20);

        result.DepartmentId.Should().Be(20);
        courseAccessService.Verify(service => service.ReconcileCourseAssignmentsAsync(course.Id), Times.Once);
        unitOfWork.Verify(item => item.BeginTransactionAsync(), Times.Once);
        unitOfWork.Verify(item => item.CommitTransactionAsync(), Times.Once);
        unitOfWork.Verify(item => item.RollbackTransactionAsync(), Times.Never);
    }
}
