using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PRN222.BLL.Services.AI;
using System.Text.Json;

namespace PRN222.Tests.Services;

public class HuggingFaceFineTunedModelServiceTests
{
    [Fact]
    public async Task GenerateAnswerAsync_ShouldRejectUnsupportedServerlessHfInferenceConfiguration()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["FineTunedModel:ModelId"] = "PhamNghia123/prn222-mt5-base",
            ["FineTunedModel:EndpointTemplate"] = "https://router.huggingface.co/hf-inference/models/{modelId}"
        });

        var act = () => service.GenerateAnswerAsync("tcp là gì?");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*hf-inference*not supported*Inference Endpoint*");
    }

    [Fact]
    public async Task GenerateAnswerAsync_ShouldRequireDedicatedEndpointUrlWhenNoEndpointIsConfigured()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["FineTunedModel:ModelId"] = "PhamNghia123/prn222-mt5-base"
        });

        var act = () => service.GenerateAnswerAsync("tcp là gì?");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*FineTunedModel:EndpointUrl is not configured*");
    }

    [Fact]
    public async Task GenerateAnswerAsync_ShouldUseDedicatedEndpointUrlWhenConfigured()
    {
        var handler = new CapturingHandler("""[{ "generated_text": "TCP là giao thức điều khiển truyền vận." }]""");
        var service = CreateService(new Dictionary<string, string?>
        {
            ["FineTunedModel:ModelId"] = "PhamNghia123/prn222-mt5-base",
            ["FineTunedModel:EndpointUrl"] = "https://prn222-mt5.example.endpoints.huggingface.cloud"
        }, handler);

        var answer = await service.GenerateAnswerAsync("tcp là gì?");

        answer.Should().Be("TCP là giao thức điều khiển truyền vận.");
        handler.RequestUri.Should().Be("https://prn222-mt5.example.endpoints.huggingface.cloud/");
        handler.RequestBody.Should().NotBeNull();

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var inputs = payload.RootElement.GetProperty("inputs").GetString();
        inputs.Should().Be("tcp là gì?");
        inputs.Should().NotContain("[INST]");
        inputs.Should().NotContain("Trả lời ngắn gọn");
    }

    private static HuggingFaceFineTunedModelService CreateService(
        Dictionary<string, string?> settings,
        HttpMessageHandler? handler = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new HuggingFaceFineTunedModelService(
            new HttpClient(handler ?? new CapturingHandler("[]")),
            configuration,
            NullLogger<HuggingFaceFineTunedModelService>.Instance);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public CapturingHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        public string? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            RequestBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody)
            };
        }
    }
}
