using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using LoLCoach.Services;
using Xunit;

namespace LoLCoach.Tests;

public class StartupApiTests
{
    [Fact]
    public async Task StartupAsksASimpleTextQuestionWithoutAnImage()
    {
        var transport = new ProbeTransport();
        using var model = new FoundryCoachingService(new(), "fake-key", new HttpClient(transport));
        Assert.Equal("Disponible.", await model.ProbeAsync(default));
        using var body = JsonDocument.Parse(transport.Body);
        Assert.False(body.RootElement.TryGetProperty("stream", out var stream) && stream.GetBoolean());
        Assert.DoesNotContain("image_url", transport.Body);
        Assert.DoesNotContain("base64", transport.Body);
    }

    [Fact]
    public async Task DiagnosticsRecordStatusAndPathButNeverSecretsOrBody()
    {
        var events = new List<ApiDiagnostic>();
        using var client = new HttpClient(new ApiDiagnosticsHandler("Azure OpenAI", events.Add, new ProbeTransport()));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://test.openai.azure.com/openai/v1/chat/completions?api-key=fake-query-secret");
        request.Headers.Add("api-key", "fake-header-secret");
        request.Content = new StringContent("fake-private-image-and-chat");
        using var response = await client.SendAsync(request);
        var entry = Assert.Single(events);
        Assert.Equal(200, entry.StatusCode);
        Assert.Equal("/openai/v1/chat/completions", entry.Path);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(entry));
        Assert.DoesNotContain("private", JsonSerializer.Serialize(entry));
    }

    [Fact]
    public void DetectionCadenceIsFiveSeconds()
        => Assert.Equal(TimeSpan.FromSeconds(5), PracticeStartDetector.PollInterval);

    [Fact]
    public void TruncatedOutputHasAnActionableSafeDiagnostic()
    {
        var exception = new ModelOutputException(OpenAI.Chat.ChatFinishReason.Length);
        Assert.Contains("límite de tokens", exception.Message);
    }

    private sealed class ProbeTransport : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"probe","object":"chat.completion","created":1,"model":"test","choices":[{"index":0,"message":{"role":"assistant","content":"Disponible."},"finish_reason":"stop"}]}""",
                    Encoding.UTF8, "application/json")
            };
        }
    }
}