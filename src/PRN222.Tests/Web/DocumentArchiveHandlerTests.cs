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

public class DocumentArchiveHandlerTests
{
    [Fact]
    public async Task OnPostArchiveAsync_ShouldForbidNonAdminWithoutCallingService()
    {
        var documentService = new Mock<IDocumentService>();
        var model = CreateModel(documentService, ApplicationRoles.HeadLecturer, "head-user-id");

        var result = await model.OnPostArchiveAsync(40, "Lý do hợp lệ");

        result.Should().BeOfType<ForbidResult>();
        documentService.Verify(
            service => service.ArchiveDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostArchiveAsync_ShouldUseAuthenticatedUserIdAsActor()
    {
        var documentService = new Mock<IDocumentService>();
        documentService
            .Setup(service => service.GetDocumentByIdAsync(40))
            .ReturnsAsync(new DocumentDetailDto { Id = 40, CourseId = 1 });
        var model = CreateModel(documentService, ApplicationRoles.Admin, "admin-claim-id");

        var result = await model.OnPostArchiveAsync(40, "Lý do từ form");

        result.Should().BeOfType<RedirectResult>();
        documentService.Verify(
            service => service.ArchiveDocumentAsync(40, "admin-claim-id", "Lý do từ form"),
            Times.Once);
    }

    private static DetailsModel CreateModel(
        Mock<IDocumentService> documentService,
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
            Mock.Of<ICourseAccessService>(),
            Mock.Of<ILogger<DetailsModel>>())
        {
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, tempDataProvider.Object)
        };
    }
}
