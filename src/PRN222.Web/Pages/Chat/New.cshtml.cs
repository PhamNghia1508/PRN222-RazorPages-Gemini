using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Chat;

[Authorize(Roles = ApplicationRoles.ChatUsers)]
public class NewModel : PageModel
{
    public IActionResult OnGet() => Redirect("/Chat/Session");
}
