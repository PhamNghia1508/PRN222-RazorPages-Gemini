using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Chat;

[Authorize(Roles = ApplicationRoles.ChatUsers)]
public class IndexModel(
    IChatService chatService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public IReadOnlyList<ChatSessionDto> Sessions { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Sessions = (await chatService.GetSessionsAsync(GetRequiredUserId())).ToList();
    }

    private string GetRequiredUserId()
    {
        return userManager.GetUserId(User)
            ?? throw new InvalidOperationException("Authenticated user id was not found.");
    }
}
