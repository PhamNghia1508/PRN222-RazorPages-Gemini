namespace PRN222.BLL.Services.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
    int Dimensions { get; }
    string ModelName { get; }
}
