namespace PRN222.BLL.Services.Interfaces;

public interface ICodeExecutionService
{
    Task<string> ExecuteCSharpAsync(string code);
}
