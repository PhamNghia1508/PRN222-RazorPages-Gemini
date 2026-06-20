namespace PRN222.BLL.Services.Interfaces;

public interface IFineTunedModelService
{
    Task<string> GenerateAnswerAsync(string question);
}
