using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Chat;

[Authorize(Roles = ApplicationRoles.ChatUsers)]
public class SessionModel(
    ICourseService courseService,
    ICourseAccessService courseAccessService,
    IChatService chatService,
    UserManager<ApplicationUser> userManager,
    ILogger<SessionModel> logger,
    IDocumentService documentService) : PageModel
{
    public IReadOnlyList<ChatMessageDto> Messages { get; private set; } = [];
    public IReadOnlyList<CourseDto> Courses { get; private set; } = [];
    public int? SessionId { get; private set; }
    public string SessionTitle { get; private set; } = "Phien chat RAG moi";

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
        {
            await LoadNewSessionAsync();
            return Page();
        }

        var userId = GetRequiredUserId();
        var session = await chatService.GetSessionByIdAsync(id.Value, userId);
        if (session == null)
        {
            return NotFound();
        }

        Messages = (await chatService.GetMessagesBySessionIdAsync(id.Value, userId)).ToList();
        Courses = (await GetVisibleCoursesAsync()).ToList();
        SessionId = id.Value;
        SessionTitle = session.SessionTitle;

        return Page();
    }

    public async Task<IActionResult> OnGetNewAsync()
    {
        await LoadNewSessionAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAskAsync([FromBody] AskQuestionDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Question) || dto.CourseId <= 0)
        {
            return BadRequest(new { error = "Cau hoi va CourseId la bat buoc." });
        }

        if (!await CanAccessCourseAsync(dto.CourseId))
        {
            return Forbid();
        }

        try
        {
            dto.UserId = GetRequiredUserId();
            var answerDto = await chatService.AskQuestionAsync(dto);
            return new JsonResult(answerDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat ask failed for user {UserId}.", userManager.GetUserId(User));
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return new JsonResult(new { error = "Khong the xu ly cau hoi luc nay. Vui long thu lai sau." });
        }
    }

    public async Task<IActionResult> OnGetCourseDocumentsAsync(int courseId)
    {
        if (courseId <= 0)
        {
            return BadRequest(new { error = "CourseId khong hop le." });
        }

        if (!await CanAccessCourseAsync(courseId))
        {
            return Forbid();
        }

        try
        {
            var documents = await documentService.GetDocumentsByCourseAsync(courseId);
            var result = documents.Select(d => new
            {
                id = d.Id,
                name = d.OriginalFileName ?? d.FileName,
                status = d.Status.ToString()
            }).ToList();
            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get documents for course {CourseId}.", courseId);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return new JsonResult(new { error = "Khong the lay danh sach hoc lieu luc nay." });
        }
    }

    public async Task<IActionResult> OnGetCitationImageAsync(int chunkId)
    {
        if (chunkId <= 0)
        {
            return BadRequest();
        }

        var image = await documentService.GetChunkImageAsync(chunkId);
        if (image == null)
        {
            return NotFound();
        }

        if (!await CanAccessCourseAsync(image.CourseId))
        {
            return Forbid();
        }

        return File(image.Data, image.ContentType);
    }

    public async Task<IActionResult> OnPostFeedbackAsync([FromBody] FeedbackRequest request)
    {
        if (request == null || request.MessageId <= 0)
        {
            return BadRequest(new { error = "MessageId khong hop le." });
        }

        var success = await chatService.SubmitFeedbackAsync(
            request.MessageId,
            request.IsHelpful,
            GetRequiredUserId());

        if (!success)
        {
            return NotFound(new { error = "Khong tim thay tin nhan." });
        }

        return new JsonResult(new { success = true });
    }

    private async Task LoadNewSessionAsync()
    {
        Messages = [];
        Courses = (await GetVisibleCoursesAsync()).ToList();
        SessionId = null;
        SessionTitle = "Phien chat RAG moi";
    }

    private string GetRequiredUserId()
    {
        return userManager.GetUserId(User)
            ?? throw new InvalidOperationException("Authenticated user id was not found.");
    }

    private bool IsCourseScopedUser() =>
        User?.Identity?.IsAuthenticated == true &&
        !User.IsInRole(ApplicationRoles.Admin) &&
        !User.IsInRole(ApplicationRoles.Student) &&
        (User.IsInRole(ApplicationRoles.HeadLecturer) || User.IsInRole(ApplicationRoles.Lecturer));

    private async Task<IEnumerable<CourseDto>> GetVisibleCoursesAsync()
    {
        var courses = (await courseService.GetAllCoursesAsync()).ToList();
        if (!IsCourseScopedUser())
        {
            return courses;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var assignedCourseIds = await courseAccessService.GetAccessibleStaffCourseIdsAsync(userId);
        return courses.Where(course => assignedCourseIds.Contains(course.Id)).ToList();
    }

    private async Task<bool> CanAccessCourseAsync(int courseId)
    {
        if (!IsCourseScopedUser())
        {
            return true;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await courseAccessService.CanStaffAccessCourseAsync(userId, courseId);
    }

    public sealed class FeedbackRequest
    {
        public int MessageId { get; set; }
        public bool IsHelpful { get; set; }
    }
}
