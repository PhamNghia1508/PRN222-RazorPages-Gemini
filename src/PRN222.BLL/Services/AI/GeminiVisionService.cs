using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services.AI;

/// <summary>
/// Implementation of Gemini Vision Service for image analysis.
/// </summary>
public class GeminiVisionService : IGeminiVisionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly ILogger<GeminiVisionService> _logger;

    public GeminiVisionService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiVisionService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini:ApiKey not found.");
        _modelName = configuration["Gemini:VisionModelName"]
            ?? configuration["Gemini:ModelName"]
            ?? throw new ArgumentNullException("Gemini vision model name not found.");
        _logger = logger;
    }

    public async Task<string> DescribeImageAsync(byte[] imageBytes, string contextHint)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";

        var base64Image = Convert.ToBase64String(imageBytes);
        
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = $"Bạn là một chuyên gia trợ giảng. Hãy mô tả chi tiết hình ảnh này trong ngữ cảnh học thuật: {contextHint}. Nếu đây là sơ đồ (UML, Database, Flowchart), hãy giải thích các thành phần và mối quan hệ giữa chúng. Trả về kết quả bằng tiếng Việt, súc tích, định dạng Markdown." },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = DetectImageMimeType(imageBytes),
                                data = base64Image
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.4,
                maxOutputTokens = 2048
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini Vision API error: {Error}", error);
                return "Không thể phân tích hình ảnh này.";
            }

            var result = await response.Content.ReadFromJsonAsync<JsonDocument>();
            var text = result?.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? "Không có mô tả cho hình ảnh này.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling Gemini Vision API");
            return "Lỗi khi xử lý hình ảnh với AI.";
        }
    }

    private static string DetectImageMimeType(byte[] imageBytes)
    {
        if (imageBytes.Length >= 3 &&
            imageBytes[0] == 0xFF &&
            imageBytes[1] == 0xD8 &&
            imageBytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (imageBytes.Length >= 12 &&
            imageBytes[0] == (byte)'R' &&
            imageBytes[1] == (byte)'I' &&
            imageBytes[2] == (byte)'F' &&
            imageBytes[3] == (byte)'F' &&
            imageBytes[8] == (byte)'W' &&
            imageBytes[9] == (byte)'E' &&
            imageBytes[10] == (byte)'B' &&
            imageBytes[11] == (byte)'P')
        {
            return "image/webp";
        }

        if (imageBytes.Length >= 6 &&
            imageBytes[0] == (byte)'G' &&
            imageBytes[1] == (byte)'I' &&
            imageBytes[2] == (byte)'F')
        {
            return "image/gif";
        }

        return "image/png";
    }
}
