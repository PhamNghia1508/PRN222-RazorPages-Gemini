using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.AI;

public class HuggingFaceFineTunedModelService : IFineTunedModelService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HuggingFaceFineTunedModelService> _logger;

    public HuggingFaceFineTunedModelService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<HuggingFaceFineTunedModelService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateAnswerAsync(string question)
    {
        var modelId = _configuration["FineTunedModel:ModelId"];
        if (string.IsNullOrWhiteSpace(modelId))
            throw new InvalidOperationException("FineTunedModel:ModelId is not configured.");

        var apiToken = _configuration["FineTunedModel:ApiToken"]
            ?? _configuration["HuggingFace:ApiToken"]
            ?? string.Empty;
        var maxNewTokens = GetIntSetting("FineTunedModel:MaxNewTokens", 512);
        var temperature = GetFloatSetting("FineTunedModel:Temperature", 0.2f);
        var endpoint = ResolveEndpoint(modelId);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrWhiteSpace(apiToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var payload = new
        {
            inputs = question,
            parameters = new
            {
                max_new_tokens = maxNewTokens,
                return_full_text = false,
                temperature
            }
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = ExtractErrorMessage(body);
                _logger.LogError("Fine-tuned HuggingFace model error ({StatusCode}): {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Fine-tuned model call failed: {response.StatusCode}. {error}");
            }

            return ExtractGeneratedText(body);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error calling fine-tuned HuggingFace model {ModelId}", modelId);
            throw new InvalidOperationException("Fine-tuned model call failed.", ex);
        }
    }

    private static string ExtractGeneratedText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            var first = root[0];
            if (first.TryGetProperty("generated_text", out var generatedText))
                return generatedText.GetString()?.Trim() ?? string.Empty;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("generated_text", out var generatedText))
                return generatedText.GetString()?.Trim() ?? string.Empty;

            if (root.TryGetProperty("error", out var error))
                throw new InvalidOperationException(error.GetString() ?? "Fine-tuned model returned an error.");
        }

        return json.Trim();
    }

    private static string ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("error", out var error))
            {
                return error.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            return body.Length <= 200 ? body : body[..200] + "...";
        }

        return body.Length <= 200 ? body : body[..200] + "...";
    }

    private string ResolveEndpoint(string modelId)
    {
        var endpointUrl = _configuration["FineTunedModel:EndpointUrl"];
        if (!string.IsNullOrWhiteSpace(endpointUrl))
            return endpointUrl;

        var endpointTemplate = _configuration["FineTunedModel:EndpointTemplate"];
        if (string.IsNullOrWhiteSpace(endpointTemplate))
        {
            throw new InvalidOperationException(
                "FineTunedModel:EndpointUrl is not configured. Deploy the fine-tuned model to a HuggingFace Inference Endpoint and set its endpoint URL.");
        }

        var endpoint = endpointTemplate.Replace("{modelId}", modelId, StringComparison.OrdinalIgnoreCase);
        if (endpoint.Contains("router.huggingface.co/hf-inference", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "FineTunedModel is configured to use HuggingFace hf-inference, but this fine-tuned model is not supported by that provider. Deploy it to a dedicated HuggingFace Inference Endpoint and set FineTunedModel:EndpointUrl.");
        }

        return endpoint;
    }

    private int GetIntSetting(string key, int defaultValue)
    {
        return int.TryParse(_configuration[key], out var parsed) ? parsed : defaultValue;
    }

    private float GetFloatSetting(string key, float defaultValue)
    {
        return float.TryParse(_configuration[key], out var parsed) ? parsed : defaultValue;
    }
}
