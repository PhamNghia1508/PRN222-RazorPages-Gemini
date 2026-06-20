using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PRN222.BLL.Services.AI;

namespace PRN222.Tests.Services;

public class GeminiVisionServiceTests
{
    [Fact]
    public async Task DescribeImageAsync_ShouldUseConfiguredModelAndDetectedMimeType()
    {
        var handler = new RecordingHandler();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-key",
                ["Gemini:VisionModelName"] = "configured-vision-model"
            })
            .Build();
        var service = new GeminiVisionService(
            new HttpClient(handler),
            configuration,
            new Mock<ILogger<GeminiVisionService>>().Object);
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0];

        var result = await service.DescribeImageAsync(jpegBytes, "diagram");

        result.Should().Be("Description");
        handler.RequestUri.Should().Contain("/models/configured-vision-model:generateContent");
        handler.RequestBody.Should().Contain("\"mime_type\":\"image/jpeg\"");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string RequestUri { get; private set; } = string.Empty;
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString() ?? string.Empty;
            RequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"candidates":[{"content":{"parts":[{"text":"Description"}]}}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
