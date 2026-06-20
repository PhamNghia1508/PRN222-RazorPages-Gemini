using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.DAL.Entities;
using PRN222.Web.Infrastructure;
using PRN222.Web.Models.Auth;

namespace PRN222.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public LoginViewModel Input { get; set; } = new();

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

        var result = await signInManager.PasswordSignInAsync(
            Input.Email,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Email hoac mat khau khong dung.");
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Input.Email);
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
