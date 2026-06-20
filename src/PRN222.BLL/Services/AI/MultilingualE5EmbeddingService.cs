using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.AI;

/// <summary>
/// Embedding service using HuggingFace Inference API with the intfloat/multilingual-e5-base model.
/// Free tier — subject to cold-start loading times (HTTP 503).
/// </summary>
public class MultilingualE5EmbeddingService : IEmbeddingService
{
    private const string Endpoint = "https://api-inference.huggingface.co/models/intfloat/multilingual-e5-base";
    private const int MaxRetries = 5;

    private readonly HttpClient _httpClient;
    private readonly string _apiToken;
    private readonly ILogger<MultilingualE5EmbeddingService> _logger;

    public int Dimensions => 768;
    public string ModelName => "multilingual-e5-base";

    public MultilingualE5EmbeddingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MultilingualE5EmbeddingService> logger)
    {
        _httpClient = httpClient;
        _apiToken = configuration["HuggingFace:ApiToken"] ?? string.Empty;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new float[Dimensions];

        // e5 models require the "query: " prefix for encoding queries
        string prefixedText = text.StartsWith("query: ", StringComparison.OrdinalIgnoreCase)
            ? text
            : $"query: {text}";

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);

                if (!string.IsNullOrWhiteSpace(_apiToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
                }

                var payload = new { inputs = prefixedText };
                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                // Handle model cold-start (503 Service Unavailable → model is loading)
                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    var body = await response.Content.ReadAsStringAsync();

                    if (body.Contains("loading", StringComparison.OrdinalIgnoreCase))
                    {
                        // Parse estimated_time if available, otherwise default to 20s
                        int waitSeconds = 20;
                        try
                        {
                            using var doc = JsonDocument.Parse(body);
                            if (doc.RootElement.TryGetProperty("estimated_time", out var et))
                            {
                                waitSeconds = Math.Max(5, (int)Math.Ceiling(et.GetDouble()));
                            }
                        }
                        catch { /* ignore parse failure */ }

                        _logger.LogWarning(
                            "HuggingFace model loading. Waiting {Wait}s before retry {Attempt}/{Max}...",
                            waitSeconds, attempt, MaxRetries);

                        await Task.Delay(waitSeconds * 1000);
                        continue;
                    }
                }

                // Handle rate limiting
                if ((int)response.StatusCode == 429)
                {
                    if (attempt == MaxRetries)
                    {
                        throw new InvalidOperationException(
                            $"HuggingFace rate limit exceeded after {MaxRetries} attempts.");
                    }

                    _logger.LogWarning("HuggingFace rate limit (429). Waiting 30s before retry {Attempt}/{Max}...",
                        attempt, MaxRetries);
                    await Task.Delay(30_000);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(
                        $"HuggingFace embedding API error ({response.StatusCode}): {errorBody}");
                }

                // Response format: [[float, float, ...]] — nested array
                var responseJson = await response.Content.ReadAsStringAsync();
                using var resultDoc = JsonDocument.Parse(responseJson);
                var root = resultDoc.RootElement;

                // Handle nested array [[...]]
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var innerArray = root[0];

                    if (innerArray.ValueKind == JsonValueKind.Array)
                    {
                        var vector = new float[innerArray.GetArrayLength()];
                        for (int i = 0; i < vector.Length; i++)
                        {
                            vector[i] = innerArray[i].GetSingle();
                        }
                        return vector;
                    }
                }

                throw new InvalidOperationException("HuggingFace embedding API returned an unexpected response format.");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning("HuggingFace request timed out. Retry {Attempt}/{Max}...", attempt, MaxRetries);
                if (attempt == MaxRetries)
                {
                    throw new InvalidOperationException(
                        $"HuggingFace embedding request timed out after {MaxRetries} attempts.", ex);
                }
                await Task.Delay(5000);
            }
            catch (Exception ex)
            {
                if (attempt == MaxRetries)
                {
                    _logger.LogError(ex, "Exception calling HuggingFace API after {Max} attempts.", MaxRetries);
                    throw new InvalidOperationException(
                        $"HuggingFace embedding API failed after {MaxRetries} attempts: {ex.Message}", ex);
                }

                _logger.LogWarning(ex, "HuggingFace API exception. Retrying in 5s...");
                await Task.Delay(5000);
            }
        }

        throw new InvalidOperationException($"HuggingFace embedding API failed after {MaxRetries} attempts.");
    }
}
