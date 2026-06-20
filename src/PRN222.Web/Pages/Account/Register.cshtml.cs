using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.DAL.Entities;
using PRN222.Web.Infrastructure;
using PRN222.Web.Models.Auth;

namespace PRN222.Web.Pages.Account;

[AllowAnonymous]
public class RegisterModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public RegisterViewModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        Input.ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = Input.ReturnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email
        };

        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        await userManager.AddToRoleAsync(user, ApplicationRoles.Student);
        await signInManager.SignInAsync(user, isPersistent: false);
        return await RedirectAfterSignInAsync(user, Input.ReturnUrl);
    }

    private async Task<IActionResult> RedirectAfterSignInAsync(ApplicationUser? user, string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        if (user is not null &&
            (await userManager.IsInRoleAsync(user, ApplicationRoles.Admin) ||
             await userManager.IsInRoleAsync(user, ApplicationRoles.HeadLecturer) ||
             await userManager.IsInRoleAsync(user, ApplicationRoles.Lecturer)))
        {
            return Redirect("/");
        }

        return Redirect("/Chat/Session");
    }
}
