using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services;

public class RoslynCodeExecutionService : ICodeExecutionService
{
    public async Task<string> ExecuteCSharpAsync(string code)
    {
        var output = new StringBuilder();
        var originalOut = Console.Out;
        
        using var sw = new StringWriter(output);
        Console.SetOut(sw);

        try
        {
            var options = ScriptOptions.Default
                .WithReferences(typeof(object).Assembly, typeof(Enumerable).Assembly, typeof(Console).Assembly)
                .WithImports("System", "System.Collections.Generic", "System.Linq", "System.Text");

            // Wrap code to capture result if it's just an expression
            // Or just run it. We use a 5s timeout.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var executeTask = Task.Run(() => CSharpScript.RunAsync(code, options, cancellationToken: cts.Token));
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

            var completedTask = await Task.WhenAny(executeTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                cts.Cancel();
                return "Execution timed out (5 seconds limit).";
            }

            await executeTask; // Throws if script failed

            var result = output.ToString();
            return string.IsNullOrWhiteSpace(result) ? "(Success, no output)" : result;
        }
        catch (CompilationErrorException ex)
        {
            if (ex.Message.Contains("CS7021"))
            {
                return $"Error: {ex.Message}\n\n💡 Hướng dẫn sửa: Trình chạy thử (C# Script Sandbox) không cho phép khai báo 'namespace'. Vui lòng bỏ dòng 'namespace ... {{' và dấu ngoặc nhọn đóng '}}' tương ứng ở cuối file. Bạn có thể viết các câu lệnh trực tiếp hoặc định nghĩa class trực tiếp mà không cần bọc trong namespace.";
            }
            return $"Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
