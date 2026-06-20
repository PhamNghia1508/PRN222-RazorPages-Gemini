using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.AI;

/// <summary>
/// Embedding service using OpenAI's text-embedding-3-small model.
/// Requires a valid API key configured at OpenAI:ApiKey.
/// </summary>
public class OpenAIEmbeddingService : IEmbeddingService
{
    private const string Endpoint = "https://api.openai.com/v1/embeddings";
    private const int MaxRetries = 3;

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<OpenAIEmbeddingService> _logger;

    public int Dimensions => 1536;
    public string ModelName => "text-embedding-3-small";

    public OpenAIEmbeddingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAIEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["OpenAI:ApiKey"] ?? string.Empty;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new float[Dimensions];

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var payload = new
                {
                    input = text,
                    model = "text-embedding-3-small"
                };

                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                // Handle rate limiting (429)
                if ((int)response.StatusCode == 429)
                {
                    if (attempt == MaxRetries)
                    {
                        _logger.LogError("OpenAI rate limit exceeded after {Max} attempts.", MaxRetries);
                        throw new InvalidOperationException($"OpenAI rate limit exceeded after {MaxRetries} attempts.");
                    }

                    // Respect Retry-After header if present
                    int waitSeconds = 30;
                    if (response.Headers.RetryAfter?.Delta is { } delta)
                    {
                        waitSeconds = Math.Max(1, (int)delta.TotalSeconds);
                    }

                    _logger.LogWarning("OpenAI rate limit (429). Waiting {Wait}s before retry {Attempt}/{Max}...",
                        waitSeconds, attempt, MaxRetries);
                    await Task.Delay(waitSeconds * 1000);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OpenAI Embedding API error ({Status}): {Body}", response.StatusCode, errorBody);
                    throw new InvalidOperationException($"OpenAI embedding API error ({response.StatusCode}).");
                }

                // Response format: { "data": [{ "embedding": [float, ...], ... }], ... }
                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var dataArray) &&
                    dataArray.GetArrayLength() > 0)
                {
                    var firstItem = dataArray[0];
                    if (firstItem.TryGetProperty("embedding", out var embeddingArray))
                    {
                        var vector = new float[embeddingArray.GetArrayLength()];
                        for (int i = 0; i < vector.Length; i++)
                        {
                            vector[i] = embeddingArray[i].GetSingle();
                        }
                        return vector;
                    }
                }

                throw new InvalidOperationException("OpenAI embedding API returned an unexpected response format.");
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("OpenAI request timed out. Retry {Attempt}/{Max}...", attempt, MaxRetries);
                if (attempt == MaxRetries)
                {
                    throw new InvalidOperationException($"OpenAI embedding request timed out after {MaxRetries} attempts.");
                }
                await Task.Delay(5000);
            }
            catch (Exception ex)
            {
                if (attempt == MaxRetries)
                {
                    _logger.LogError(ex, "Exception calling OpenAI Embedding API after {Max} attempts.", MaxRetries);
                    throw new InvalidOperationException($"OpenAI embedding API failed after {MaxRetries} attempts.", ex);
                }

                _logger.LogWarning(ex, "OpenAI API exception. Retrying in 5s...");
                await Task.Delay(5000);
            }
        }

        throw new InvalidOperationException($"OpenAI embedding API failed after {MaxRetries} attempts.");
    }
}
