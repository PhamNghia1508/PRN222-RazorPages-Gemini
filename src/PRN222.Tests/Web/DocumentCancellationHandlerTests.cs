using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Infrastructure;
using PRN222.Web.Pages.Document;

namespace PRN222.Tests.Web;

public class DocumentCancellationHandlerTests
{
    [Theory]
    [InlineData(ApplicationRoles.Admin)]
    [InlineData(ApplicationRoles.Lecturer)]
    [InlineData(ApplicationRoles.Student)]
    public async Task OnPostCancelUploadAsync_ShouldForbidRolesOutsideHeadLecturer(string role)
    {
        var documentService = new Mock<IDocumentService>();
        var courseAccessService = new Mock<ICourseAccessService>();
        var model = CreateModel(documentService, courseAccessService, role, "actor-id");

        var result = await model.OnPostCancelUploadAsync(41, "tai nham");

        result.Should().BeOfType<ForbidResult>();
        documentService.Verify(
            service => service.CancelMistakenUploadAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostCancelUploadAsync_ShouldUseAuthenticatedUserIdAsActor()
    {
        var documentService = new Mock<IDocumentService>();
        var courseAccessService = new Mock<ICourseAccessService>();
        documentService
            .Setup(service => service.GetDocumentByIdAsync(41))
            .ReturnsAsync(new DocumentDetailDto
            {
                Id = 41,
                CourseId = 10,
                UploadedByUserId = "head-claim-id",
                Status = "Uploaded"
            });
        courseAccessService
            .Setup(service => service.CanStaffAccessCourseAsync("head-claim-id", 10))
            .ReturnsAsync(true);
        var model = CreateModel(
            documentService,
            courseAccessService,
            ApplicationRoles.HeadLecturer,
            "head-claim-id");

        var result = await model.OnPostCancelUploadAsync(41, "  tai nham phien ban  ");

        result.Should().BeOfType<RedirectResult>();
        documentService.Verify(
            service => service.CancelMistakenUploadAsync(
                41,
                "head-claim-id",
                "  tai nham phien ban  "),
            Times.Once);
    }

    [Fact]
    public async Task OnPostCancelUploadAsync_ShouldForbidForgedOwnerBeforeCallingService()
    {
        var documentService = new Mock<IDocumentService>();
        var courseAccessService = new Mock<ICourseAccessService>();
        documentService
            .Setup(service => service.GetDocumentByIdAsync(41))
            .ReturnsAsync(new DocumentDetailDto
            {
                Id = 41,
                CourseId = 10,
                UploadedByUserId = "other-user-id",
                Status = "Uploaded"
            });
        courseAccessService
            .Setup(service => service.CanStaffAccessCourseAsync("head-claim-id", 10))
            .ReturnsAsync(true);
        var model = CreateModel(
            documentService,
            courseAccessService,
            ApplicationRoles.HeadLecturer,
            "head-claim-id");

        var result = await model.OnPostCancelUploadAsync(41, "tai nham");

        result.Should().BeOfType<ForbidResult>();
        documentService.Verify(
            service => service.CancelMistakenUploadAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostCancelUploadAsync_ShouldForbidCourseOutsideScopeBeforeCallingService()
    {
        var documentService = new Mock<IDocumentService>();
        var courseAccessService = new Mock<ICourseAccessService>();
        documentService
            .Setup(service => service.GetDocumentByIdAsync(41))
            .ReturnsAsync(new DocumentDetailDto
            {
                Id = 41,
                CourseId = 99,
                UploadedByUserId = "head-claim-id",
                Status = "Uploaded"
            });
        courseAccessService
            .Setup(service => service.CanStaffAccessCourseAsync("head-claim-id", 99))
            .ReturnsAsync(false);
        var model = CreateModel(
            documentService,
            courseAccessService,
            ApplicationRoles.HeadLecturer,
            "head-claim-id");

        var result = await model.OnPostCancelUploadAsync(41, "tai nham");

        result.Should().BeOfType<ForbidResult>();
        documentService.Verify(
            service => service.CancelMistakenUploadAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    private static DetailsModel CreateModel(
        Mock<IDocumentService> documentService,
        Mock<ICourseAccessService> courseAccessService,
        string role,
        string userId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        ], "Test");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        var tempDataProvider = new Mock<ITempDataProvider>();
        tempDataProvider.Setup(provider => provider.LoadTempData(httpContext))
            .Returns(new Dictionary<string, object>());

        return new DetailsModel(
            documentService.Object,
            courseAccessService.Object,
            Mock.Of<ILogger<DetailsModel>>())
        {
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, tempDataProvider.Object)
        };
    }
}
