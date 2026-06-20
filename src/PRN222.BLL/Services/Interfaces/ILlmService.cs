namespace PRN222.BLL.Services.Interfaces;

public interface ILlmService
{
    Task<string> GenerateAnswerAsync(string prompt, string context);
    
    /// <summary>
    /// Generates Question-Answer pairs from a given chunk of text.
    /// Expected to return a JSON string representing an array of QAPairDto.
    /// </summary>
    Task<string> GenerateQAPairsAsync(string chunkText, int count = 3);
}
