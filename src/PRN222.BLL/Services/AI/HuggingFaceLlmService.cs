using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.AI;

/// <summary>
/// LLM Service using HuggingFace Inference API.
/// Defaults to Mistral-7B-Instruct-v0.3 which is excellent, fast, and free on HF API.
/// </summary>
public class HuggingFaceLlmService : ILlmService
{
    private const string DefaultModel = "mistralai/Mistral-7B-Instruct-v0.3";
    private readonly HttpClient _httpClient;
    private readonly string _apiToken;
    private readonly ILogger<HuggingFaceLlmService> _logger;
    private readonly string _endpoint;

    public HuggingFaceLlmService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<HuggingFaceLlmService> logger)
    {
        _httpClient = httpClient;
        _apiToken = configuration["HuggingFace:ApiToken"] ?? string.Empty;
        _logger = logger;
        _endpoint = $"https://api-inference.huggingface.co/models/{DefaultModel}";
    }

    public async Task<string> GenerateAnswerAsync(string prompt, string context)
    {
        // Format prompt using Mistral Instruct syntax
        string formattedPrompt = $"<s>[INST] Bạn là trợ lý học tập môn PRN222. Hãy trả lời câu hỏi dựa trên context cung cấp.\n\nContext:\n{context}\n\nQuestion:\n{prompt} [/INST]";

        return await CallHuggingFaceApiAsync(formattedPrompt, maxTokens: 1024);
    }

    public async Task<string> GenerateQAPairsAsync(string chunkText, int count = 3)
    {
        string prompt = $"<s>[INST] Dựa vào đoạn văn bản sau, hãy tạo ra {count} cặp câu hỏi và câu trả lời (ground truth) bằng tiếng Việt.\n" +
                        "Định dạng trả về BẮT BUỘC là JSON thuần túy (không bọc trong markdown ```json):\n" +
                        "[\n  { \"Question\": \"...\", \"Answer\": \"...\" }\n]\n\n" +
                        $"Đoạn văn:\n{chunkText} [/INST]";

        var response = await CallHuggingFaceApiAsync(prompt, maxTokens: 1024);
        
        // Clean up markdown if model adds it
        response = response.Replace("```json", "").Replace("```", "").Trim();
        return response;
    }

    private async Task<string> CallHuggingFaceApiAsync(string prompt, int maxTokens)
    {
        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            _logger.LogError("HuggingFace API Token is missing.");
            return "Error: Missing HuggingFace API Token.";
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

            var payload = new
            {
                inputs = prompt,
                parameters = new
                {
                    max_new_tokens = maxTokens,
                    return_full_text = false,
                    temperature = 0.2
                }
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("HuggingFace API error: {Status} - {Error}", response.StatusCode, error);
                
                // Handle cold start
                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && error.Contains("loading"))
                {
                    return "Error: Model đang được load lên server HuggingFace (Cold Start). Vui lòng đợi 20 giây rồi thử lại.";
                }
                return $"Error: Call LLM thất bại. Status: {response.StatusCode}";
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            // Inference API usually returns array: [{ "generated_text": "..." }]
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var first = doc.RootElement[0];
                if (first.TryGetProperty("generated_text", out var genText))
                {
                    return genText.GetString()?.Trim() ?? string.Empty;
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling HuggingFace API");
            return $"Error: {ex.Message}";
        }
    }
}
