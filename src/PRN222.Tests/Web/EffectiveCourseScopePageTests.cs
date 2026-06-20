using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.Web.Infrastructure;
using ChatSessionModel = PRN222.Web.Pages.Chat.SessionModel;
using DocumentUploadModel = PRN222.Web.Pages.Document.UploadModel;

namespace PRN222.Tests.Web;

public class EffectiveCourseScopePageTests
{
    [Fact]
    public async Task DocumentUploadPage_WhenHeadLecturerHasStaleCrossDepartmentAssignment_ReturnsForbid()
    {
        var documentService = new Mock<IDocumentService>();
        var courseAccess = new Mock<ICourseAccessService>();
        courseAccess.Setup(service => service.CanStaffAccessCourseAsync("head-1", 20)).ReturnsAsync(false);
        var page = new DocumentUploadModel(
            documentService.Object,
            new Mock<ICourseService>().Object,
            courseAccess.Object,
            new Mock<ILogger<DocumentUploadModel>>().Object)
        {
            PageContext = CreatePageContext("head-1", ApplicationRoles.HeadLecturer)
        };

        var result = await page.OnPostAsync(Mock.Of<IFormFile>(), 20);

        result.Should().BeOfType<ForbidResult>();
        documentService.Verify(service => service.UploadDocumentAsync(It.IsAny<DocumentUploadDto>(), It.IsAny<Stream>()), Times.Never);
    }

    [Fact]
    public async Task ChatAskPage_WhenLecturerHasStaleCrossDepartmentAssignment_ReturnsForbid()
    {
        var chatService = new Mock<IChatService>();
        var courseAccess = new Mock<ICourseAccessService>();
        courseAccess.Setup(service => service.CanStaffAccessCourseAsync("lecturer-1", 20)).ReturnsAsync(false);
        var page = new ChatSessionModel(
            new Mock<ICourseService>().Object,
            courseAccess.Object,
            chatService.Object,
            CreateUserManagerMock().Object,
            new Mock<ILogger<ChatSessionModel>>().Object,
            new Mock<IDocumentService>().Object)
        {
            PageContext = CreatePageContext("lecturer-1", ApplicationRoles.Lecturer)
        };

        var result = await page.OnPostAskAsync(new AskQuestionDto
        {
            CourseId = 20,
            Question = "MVC la gi?"
        });

        result.Should().BeOfType<ForbidResult>();
        chatService.Verify(service => service.AskQuestionAsync(It.IsAny<AskQuestionDto>()), Times.Never);
    }

    private static PageContext CreatePageContext(string userId, string role)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            ],
            "TestAuth");

        return new PageContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        return new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
    }
}
