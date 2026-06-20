using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PRN222.BLL.Services.AI;

namespace PRN222.Tests.Services;

public class GeminiEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_ShouldSendApiKeyInHeader_NotQueryString()
    {
        var handler = new RecordingEmbeddingHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        var result = await service.GenerateEmbeddingAsync("hello");

        result.Should().Equal(0.1f, 0.2f);
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Uri.Should().NotContain("key=test-key");
        handler.Requests[0].Headers.Should().ContainKey("x-goog-api-key");
        handler.Requests[0].Headers["x-goog-api-key"].Should().Equal("test-key");
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ShouldRetry429WithoutLongDelay_WhenConfigured()
    {
        var handler = new RecordingEmbeddingHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);
        var service = CreateService(handler, new Dictionary<string, string?>
        {
            ["Gemini:Embedding:MaxRetries"] = "2",
            ["Gemini:Embedding:RetryDelayMs"] = "0",
            ["Gemini:Embedding:RequestDelayMs"] = "0"
        });

        var result = await service.GenerateEmbeddingAsync("hello");

        result.Should().Equal(0.1f, 0.2f);
        handler.Requests.Should().HaveCount(2);
    }

    private static GeminiEmbeddingService CreateService(
        RecordingEmbeddingHandler handler,
        Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-key",
            ["Gemini:Embedding:RequestDelayMs"] = "0",
            ["Gemini:Embedding:RetryDelayMs"] = "0",
            ["Gemini:Embedding:MaxRetries"] = "1"
        };

        if (overrides != null)
        {
            foreach (var item in overrides)
            {
                settings[item.Key] = item.Value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new GeminiEmbeddingService(
            new HttpClient(handler),
            configuration,
            new Mock<ILogger<GeminiEmbeddingService>>().Object);
    }

    private sealed class RecordingEmbeddingHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private int _index;

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(
                h => h.Key,
                h => h.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);

            Requests.Add(new RecordedRequest(
                request.RequestUri?.ToString() ?? string.Empty,
                headers,
                request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));

            var status = statuses[Math.Min(_index++, statuses.Length - 1)];
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    """{"embedding":{"values":[0.1,0.2]}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed record RecordedRequest(
        string Uri,
        Dictionary<string, string[]> Headers,
        string Body);
}
