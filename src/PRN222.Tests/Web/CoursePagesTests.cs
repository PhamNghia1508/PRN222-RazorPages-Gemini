using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Hubs;
using PRN222.Web.Models.Course;
using CourseCreateModel = PRN222.Web.Pages.Course.CreateModel;

namespace PRN222.Tests.Web;

public class CoursePagesTests
{
    private readonly Mock<ICourseService> _courseService = new();
    private readonly Mock<IDepartmentService> _departmentService = new();
    private readonly Mock<ICourseAssignmentService> _courseAssignmentService = new();
    private readonly Mock<IHubContext<AdminHub>> _hubContext = new();
    private readonly Mock<IHubClients> _hubClients = new();
    private readonly Mock<IClientProxy> _adminClientProxy = new();
    private readonly Mock<IClientProxy> _assignedUsersClientProxy = new();
    private readonly CapturingLogger<CourseCreateModel> _logger = new();

    public CoursePagesTests()
    {
        _departmentService.Setup(service => service.GetAllAsync())
            .ReturnsAsync([
                new DepartmentDto { Id = 10, Name = "Khoa Cong nghe thong tin", Code = "CNTT" }
            ]);
        _departmentService.Setup(service => service.GetByIdAsync(10))
            .ReturnsAsync(new DepartmentDto { Id = 10, Name = "Khoa Cong nghe thong tin", Code = "CNTT" });
        _courseAssignmentService.Setup(service => service.GetAssignedUserIdsForCourseAsync(It.IsAny<int>()))
            .ReturnsAsync([]);
        _hubContext.SetupGet(context => context.Clients).Returns(_hubClients.Object);
        _hubClients.Setup(clients => clients.Group(AdminHub.AdminsGroup)).Returns(_adminClientProxy.Object);
        _hubClients.Setup(clients => clients.Users(It.IsAny<IReadOnlyList<string>>())).Returns(_assignedUsersClientProxy.Object);
        _adminClientProxy.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _assignedUsersClientProxy.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task CreatePage_WhenDepartmentIsMissing_ReturnsFormWithoutCreatingCourse()
    {
        var page = CreatePage();
        page.Input = new CourseFormViewModel { Name = "PRN222", Description = "ASP.NET Core", DepartmentId = null };

        var result = await page.OnPostAsync();

        result.Should().BeOfType<PageResult>();
        page.ModelState[nameof(CourseFormViewModel.DepartmentId)]!.Errors.Should().ContainSingle();
        _courseService.Verify(service => service.CreateCourseAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreatePage_WhenDepartmentIsValid_CreatesCourseInSelectedDepartment()
    {
        _courseService.Setup(service => service.CreateCourseAsync("PRN222", "ASP.NET Core", 10))
            .ReturnsAsync(new CourseDto(1, "PRN222", "ASP.NET Core", 0, DateTime.UtcNow) { DepartmentId = 10 });
        var page = CreatePage();
        page.Input = new CourseFormViewModel { Name = "PRN222", Description = "ASP.NET Core", DepartmentId = 10 };

        await page.OnPostAsync();

        _courseService.Verify(service => service.CreateCourseAsync("PRN222", "ASP.NET Core", 10), Times.Once);
    }

    [Fact]
    public async Task CreatePage_WhenDepartmentIsValid_NotifiesAdminsAndAssignedUsers()
    {
        _courseService.Setup(service => service.CreateCourseAsync("PRN222", "ASP.NET Core", 10))
            .ReturnsAsync(new CourseDto(1, "PRN222", "ASP.NET Core", 0, DateTime.UtcNow) { DepartmentId = 10 });
        _courseAssignmentService.Setup(service => service.GetAssignedUserIdsForCourseAsync(It.IsAny<int>()))
            .ReturnsAsync(["headlecturer-user-id"]);
        var page = CreatePage();
        page.Input = new CourseFormViewModel { Name = "PRN222", Description = "ASP.NET Core", DepartmentId = 10 };

        var result = await page.OnPostAsync();

        page.ModelState.Values.SelectMany(entry => entry.Errors).Select(error => error.ErrorMessage)
            .Should().BeEmpty(_logger.LastException?.ToString());
        result.Should().BeOfType<RedirectResult>();
        _courseAssignmentService.Verify(service => service.GetAssignedUserIdsForCourseAsync(It.IsAny<int>()), Times.Once);
        _adminClientProxy.Verify(proxy => proxy.SendCoreAsync(
            AdminHub.SubjectCreated,
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _assignedUsersClientProxy.Verify(proxy => proxy.SendCoreAsync(
            AdminHub.SubjectCreated,
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _hubClients.Verify(clients => clients.Users(
            It.Is<IReadOnlyList<string>>(userIds => userIds.SequenceEqual(new[] { "headlecturer-user-id" }))), Times.Once);
    }

    private CourseCreateModel CreatePage()
    {
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.SetupGet(helper => helper.ActionContext)
            .Returns(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()));
        urlHelper.Setup(helper => helper.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Returns("/Course?handler=AssignedCoursesPartial");

        var page = new CourseCreateModel(
            _courseService.Object,
            _departmentService.Object,
            _courseAssignmentService.Object,
            _hubContext.Object,
            _logger)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new Mock<ITempDataProvider>().Object),
            Url = urlHelper.Object
        };

        return page;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public Exception? LastException { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LastException = exception;
        }
    }
}
