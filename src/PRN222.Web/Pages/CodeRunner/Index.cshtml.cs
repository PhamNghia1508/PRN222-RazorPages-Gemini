using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.Web.Pages.CodeRunner;

[Authorize]
public class IndexModel(
    ICodeExecutionService codeExecutionService,
    IConfiguration configuration) : PageModel
{
    private const int MaxCodeLength = 10_000;

    public async Task<IActionResult> OnPostExecuteAsync([FromBody] CodeRequest request)
    {
        if (!configuration.GetValue<bool>("Features:CodeExecution:Enabled"))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest("Code is required.");
        }

        if (request.Code.Length > MaxCodeLength)
        {
            return BadRequest($"Code must not exceed {MaxCodeLength} characters.");
        }

        var result = await codeExecutionService.ExecuteCSharpAsync(request.Code);
        return new JsonResult(new { output = result });
    }
}

public sealed class CodeRequest
{
    public string Code { get; set; } = string.Empty;
}
