using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.AI;

public class GeminiEmbeddingService : IEmbeddingService
{
    private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent";
    private static readonly SemaphoreSlim RequestGate = new(1, 1);
    private static DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiEmbeddingService> _logger;
    private readonly int _maxRetries;
    private readonly int _retryDelayMs;
    private readonly int _requestDelayMs;

    public int Dimensions => 768; // text-embedding-004 output dimension
    public string ModelName => "gemini-embedding-001";

    public GeminiEmbeddingService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini:ApiKey not found in configuration.");
        _logger = logger;
        _maxRetries = GetIntSetting(configuration, "Gemini:Embedding:MaxRetries",
            GetIntSetting(configuration, "Gemini:RateLimit:MaxRetries", 3));
        _retryDelayMs = GetIntSetting(configuration, "Gemini:Embedding:RetryDelayMs",
            GetIntSetting(configuration, "Gemini:RateLimit:RetryDelayMs", 60000));
        _requestDelayMs = GetIntSetting(configuration, "Gemini:Embedding:RequestDelayMs",
            GetIntSetting(configuration, "Gemini:RateLimit:ChunkDelayMs", 0));
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[Dimensions];
        }
        
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var payload = new
                {
                    model = "models/gemini-embedding-001",
                    content = new
                    {
                        parts = new[] { new { text = text } }
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
                request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await SendWithThrottleAsync(request);

                // Handle rate limiting (429 Too Many Requests)
                if ((int)response.StatusCode == 429)
                {
                    if (attempt == _maxRetries)
                    {
                        _logger.LogError("Gemini Embedding API rate limit exceeded after {MaxRetries} attempts.", _maxRetries);
                        throw new InvalidOperationException($"Gemini embedding API rate limit exceeded after {_maxRetries} attempts.");
                    }

                    var delayMs = GetRetryDelayMs(response);
                    _logger.LogWarning("Gemini Embedding API rate limit hit (429). Waiting {DelayMs}ms before retry {Attempt}/{MaxRetries}...", delayMs, attempt, _maxRetries);
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs);
                    }
                    continue; // Retry
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini Embedding API error ({StatusCode}): {ErrorContent}", response.StatusCode, errorContent);
                    throw new InvalidOperationException($"Gemini embedding API error ({response.StatusCode}).");
                }

                var result = await response.Content.ReadFromJsonAsync<JsonDocument>();
                
                var embedding = result?.RootElement.GetProperty("embedding");
                if (embedding.HasValue)
                {
                    var values = embedding.Value.GetProperty("values");
                    if (values.ValueKind == JsonValueKind.Array)
                    {
                        var floatArray = new float[values.GetArrayLength()];
                        for (int i = 0; i < floatArray.Length; i++)
                        {
                            floatArray[i] = values[i].GetSingle();
                        }
                        return floatArray;
                    }
                }

                throw new InvalidOperationException("Gemini embedding API returned an unexpected response format.");
            }
            catch (Exception ex)
            {
                if (attempt == _maxRetries)
                {
                    _logger.LogError(ex, "Exception calling Gemini Embedding API after {MaxRetries} attempts.", _maxRetries);
                    throw new InvalidOperationException($"Gemini embedding API failed after {_maxRetries} attempts.", ex);
                }
                
                var delayMs = Math.Min(Math.Max(_retryDelayMs, 0), 5000);
                _logger.LogWarning(ex, "Network exception calling Gemini Embedding API. Retrying in {DelayMs}ms...", delayMs);
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs);
                }
            }
        }
        
        throw new InvalidOperationException($"Gemini embedding API failed after {_maxRetries} attempts.");
    }

    private async Task<HttpResponseMessage> SendWithThrottleAsync(HttpRequestMessage request)
    {
        await RequestGate.WaitAsync();
        try
        {
            if (_requestDelayMs > 0 && _lastRequestAt != DateTimeOffset.MinValue)
            {
                var elapsedMs = (DateTimeOffset.UtcNow - _lastRequestAt).TotalMilliseconds;
                var remainingDelayMs = _requestDelayMs - elapsedMs;
                if (remainingDelayMs > 0)
                {
                    await Task.Delay((int)remainingDelayMs);
                }
            }

            var response = await _httpClient.SendAsync(request);
            _lastRequestAt = DateTimeOffset.UtcNow;
            return response;
        }
        finally
        {
            RequestGate.Release();
        }
    }

    private int GetRetryDelayMs(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return Math.Max(0, (int)delta.TotalMilliseconds);
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return Math.Max(0, (int)wait.TotalMilliseconds);
        }

        return Math.Max(_retryDelayMs, 0);
    }

    private static int GetIntSetting(IConfiguration configuration, string key, int defaultValue)
    {
        var value = configuration[key];
        return int.TryParse(value, out var parsed) ? Math.Max(parsed, 0) : defaultValue;
    }
}
