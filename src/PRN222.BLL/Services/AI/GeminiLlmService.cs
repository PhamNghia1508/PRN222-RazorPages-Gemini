using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.AI;

public class GeminiLlmService : ILlmService
{
    private static readonly SemaphoreSlim _answerGate = new(1, 1);
    private static readonly SemaphoreSlim _qaGenerationGate = new(1, 1);
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly ILogger<GeminiLlmService> _logger;
    private readonly int _retryDelayMs;
    private readonly int _answerRetryDelayMs;
    private readonly int _maxRetries;

    public GeminiLlmService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiLlmService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini:ApiKey not found in configuration.");
        _modelName = configuration["Gemini:ModelName"] ?? "gemini-3-flash-preview";
        _logger = logger;
        _retryDelayMs = GetIntSetting(configuration, "Gemini:RateLimit:RetryDelayMs", 60000);
        _answerRetryDelayMs = GetIntSetting(configuration, "Gemini:RateLimit:AnswerRetryDelayMs", 8000);
        _maxRetries = GetIntSetting(configuration, "Gemini:RateLimit:MaxRetries", 3);

        if (_retryDelayMs < 0)
            _retryDelayMs = 0;
        if (_answerRetryDelayMs < 0)
            _answerRetryDelayMs = 0;
        if (_maxRetries < 1)
            _maxRetries = 1;
    }

    public async Task<string> GenerateAnswerAsync(string prompt, string context)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";
            
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.2 // Low temperature for more factual, context-based answers
            }
        };

        try
        {
            HttpResponseMessage response = null!;
            var attempts = Math.Min(_maxRetries, 3);

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                response = await PostAnswerRequestAsync(url, payload);

                if (!IsTransientStatusCode(response.StatusCode))
                    break;

                if (attempt < attempts)
                {
                    _logger.LogWarning(
                        "Gemini transient status {StatusCode} on answer generation. Waiting {Delay}ms before retry {Attempt}/{MaxRetries}...",
                        (int)response.StatusCode, _answerRetryDelayMs, attempt, attempts);
                    if (_answerRetryDelayMs > 0)
                        await Task.Delay(_answerRetryDelayMs);
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini API error ({StatusCode}): {ErrorContent}", response.StatusCode, errorContent);
                return "Xin lỗi, đã xảy ra lỗi khi kết nối với AI (Gemini). Vui lòng thử lại sau.";
            }

            var result = await response.Content.ReadFromJsonAsync<JsonDocument>();
            
            var candidates = result?.RootElement.GetProperty("candidates");
            if (candidates.HasValue && candidates.Value.GetArrayLength() > 0)
            {
                var firstCandidate = candidates.Value[0];
                var content = firstCandidate.GetProperty("content");
                var parts = content.GetProperty("parts");
                if (parts.GetArrayLength() > 0)
                {
                    return parts[0].GetProperty("text").GetString() ?? "";
                }
            }

            return "Không thể trích xuất câu trả lời từ Gemini.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling Gemini API");
            return await Task.FromResult("Xin lỗi, tôi không thể trả lời câu hỏi này vào lúc này.");
        }
    }

    public async Task<string> GenerateQAPairsAsync(string chunkText, int count = 3)
    {
        var prompt = $@"
Bạn là một chuyên gia giáo dục xuất sắc. Dựa vào đoạn văn bản học thuật sau đây, hãy tạo ra đúng {count} cặp câu hỏi và câu trả lời (Q&A) phản ánh những thông tin quan trọng nhất trong đoạn văn.
Câu hỏi phải cụ thể, rõ ràng và có thể trả lời hoàn toàn dựa vào đoạn văn.
Câu trả lời phải ngắn gọn 1-2 câu, súc tích và chính xác.
Câu trả lời phải dùng đúng thuật ngữ kỹ thuật xuất hiện trong đoạn văn, không thêm dẫn nhập, không markdown, không bullet point.

ĐOẠN VĂN:
{chunkText}

YÊU CẦU ĐẦU RA:
Trả về KẾT QUẢ DUY NHẤT LÀ MỘT MẢNG JSON, không có thêm bất kỳ giải thích nào. Định dạng mảng JSON phải chính xác như sau:
[
  {{
    ""question"": ""Câu hỏi 1"",
    ""answer"": ""Câu trả lời 1""
  }},
  ...
]
";

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.2, // Low temp for more factual generation
                maxOutputTokens = 4096,
                responseMimeType = "application/json" // Force JSON output if Gemini API supports it
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";

        try
        {
            HttpResponseMessage response = null!;

            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                response = await PostQaGenerationRequestAsync(apiUrl, content);

                if (!IsTransientStatusCode(response.StatusCode))
                    break;

                if (attempt < _maxRetries)
                {
                    _logger.LogWarning(
                        "Gemini transient status {StatusCode} on QA generation. Waiting {Delay}ms before retry {Attempt}/{MaxRetries}...",
                        (int)response.StatusCode, _retryDelayMs, attempt, _maxRetries);
                    if (_retryDelayMs > 0)
                        await Task.Delay(_retryDelayMs);
                }
            }

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseJson);

            var root = document.RootElement;
            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var contentObj) &&
                    contentObj.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    var textResponse = parts[0].GetProperty("text").GetString() ?? "[]";
                    
                    // Robust JSON array extraction
                    var startIndex = textResponse.IndexOf('[');
                    var endIndex = textResponse.LastIndexOf(']');
                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        textResponse = textResponse.Substring(startIndex, endIndex - startIndex + 1);
                    }
                    else
                    {
                        // Fallback in case it returns an object instead of array
                        startIndex = textResponse.IndexOf('{');
                        endIndex = textResponse.LastIndexOf('}');
                        if (startIndex >= 0 && endIndex > startIndex)
                        {
                            textResponse = "[" + textResponse.Substring(startIndex, endIndex - startIndex + 1) + "]";
                        }
                    }

                    return textResponse.Trim();
                }
            }

            return "[]";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API for QA generation");
            return "[]";
        }
    }

    private async Task<HttpResponseMessage> PostQaGenerationRequestAsync(string apiUrl, HttpContent content)
    {
        await _qaGenerationGate.WaitAsync();
        try
        {
            return await _httpClient.PostAsync(apiUrl, content);
        }
        finally
        {
            _qaGenerationGate.Release();
        }
    }

    private async Task<HttpResponseMessage> PostAnswerRequestAsync(string apiUrl, object payload)
    {
        await _answerGate.WaitAsync();
        try
        {
            return await _httpClient.PostAsJsonAsync(apiUrl, payload);
        }
        finally
        {
            _answerGate.Release();
        }
    }

    private static int GetIntSetting(IConfiguration configuration, string key, int defaultValue)
    {
        var value = configuration[key];
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests ||
               statusCode == HttpStatusCode.InternalServerError ||
               statusCode == HttpStatusCode.BadGateway ||
               statusCode == HttpStatusCode.ServiceUnavailable ||
               statusCode == HttpStatusCode.GatewayTimeout;
    }
}
