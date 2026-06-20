using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PRN222.Web.Pages.TestSet;

public class IndexModel : PageModel
{
    public IActionResult OnPostSeedAsync(int courseId = 1)
    {
        return BadRequest(new
        {
            success = false,
            message = "Manual seed is disabled. Upload/index documents and use Test Set Generator to create document-grounded QA pairs."
        });
    }

    public IActionResult OnGetPreviewAsync(int courseId = 1) =>
        Redirect($"/TestSetGenerator?courseId={courseId}");
}
