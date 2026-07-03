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

public class DocumentUploadHandlerTests
{
    [Theory]
    [InlineData(ApplicationRoles.Admin)]
    [InlineData(ApplicationRoles.Lecturer)]
    [InlineData(ApplicationRoles.Student)]
    public async Task OnPostAsync_ShouldForbidRolesOutsideHeadLecturer(string role)
    {
        var documentService = new Mock<IDocumentService>();
        var courseAccessService = new Mock<ICourseAccessService>();
        var model = CreateModel(documentService, courseAccessService, role, "user-id");

        var result = await model.OnPostAsync(CreatePdf(), 10, true);

        result.Should().BeOfType<ForbidResult>();
        documentService.Verify(
            service => service.UploadDocumentAsync(It.IsAny<DocumentUploadDto>(), It.IsAny<Stream>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostAsync_ShouldRejectForgedCourseBeforeSavingFile()
    {
        var documentService = new Mock<IDocumentService>();
        var courseAccessService = new Mock<ICourseAccessService>();
        courseAccessService
            .Setup(service => service.CanStaffAccessCourseAsync("head-user-id", 99))
            .ReturnsAsync(false);
        var model = CreateModel(
            documentService,
            courseAccessService,
            ApplicationRoles.HeadLecturer,
            "head-user-id");

        var result = await model.OnPostAsync(CreatePdf(), 99, true);

        result.Should().BeOfType<ForbidResult>();
        documentService.Verify(
            service => service.UploadDocumentAsync(It.IsAny<DocumentUploadDto>(), It.IsAny<Stream>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostAsync_ShouldRequireResponsibilityConfirmation()
    {
        var documentService = new Mock<IDocumentService>();
        var courseAccessService = new Mock<ICourseAccessService>();
        courseAccessService
            .Setup(service => service.CanStaffAccessCourseAsync("head-user-id", 10))
            .ReturnsAsync(true);
        var model = CreateModel(
            documentService,
            courseAccessService,
            ApplicationRoles.HeadLecturer,
            "head-user-id");

        var result = await model.OnPostAsync(CreatePdf(), 10, false);

        result.Should().BeOfType<PageResult>();
        model.TempData["Error"].Should().Be(
            "Bạn phải xác nhận trách nhiệm về nguồn và nội dung tài liệu trước khi tải lên.");
        documentService.Verify(
            service => service.UploadDocumentAsync(It.IsAny<DocumentUploadDto>(), It.IsAny<Stream>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostAsync_ShouldUseAuthenticatedClaimAsUploader()
    {
        var documentService = new Mock<IDocumentService>();
        var courseAccessService = new Mock<ICourseAccessService>();
        courseAccessService
            .Setup(service => service.CanStaffAccessCourseAsync("head-claim-id", 10))
            .ReturnsAsync(true);
        documentService
            .Setup(service => service.UploadDocumentAsync(
                It.IsAny<DocumentUploadDto>(),
                It.IsAny<Stream>()))
            .ReturnsAsync(new DocumentDto(
                50,
                "stored.pdf",
                "source.pdf",
                "application/pdf",
                4,
                0,
                "Uploaded",
                "PRN222",
                10,
                DateTime.UtcNow));
        var model = CreateModel(
            documentService,
            courseAccessService,
            ApplicationRoles.HeadLecturer,
            "head-claim-id");

        var result = await model.OnPostAsync(CreatePdf(), 10, true);

        result.Should().BeOfType<RedirectResult>();
        documentService.Verify(
            service => service.UploadDocumentAsync(
                It.Is<DocumentUploadDto>(dto =>
                    dto.CourseId == 10 &&
                    dto.UploadedByUserId == "head-claim-id"),
                It.IsAny<Stream>()),
            Times.Once);
    }

    private static UploadModel CreateModel(
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
        tempDataProvider
            .Setup(provider => provider.LoadTempData(httpContext))
            .Returns(new Dictionary<string, object>());

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetAllCoursesAsync())
            .ReturnsAsync(Array.Empty<CourseDto>());

        return new UploadModel(
            documentService.Object,
            courseService.Object,
            courseAccessService.Object,
            Mock.Of<ILogger<UploadModel>>())
        {
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, tempDataProvider.Object)
        };
    }

    private static IFormFile CreatePdf()
    {
        var stream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        return new FormFile(stream, 0, stream.Length, "file", "source.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }
}
