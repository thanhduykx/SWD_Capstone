using System.Net;
using System.Text;
using System.Text.Json;
using CPMS.BLL.AI.ProjectSuggestions;
using CPMS.BLL.Common.Exceptions;
using CPMS.DAL.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace CPMS.IntegrationTests;

public sealed class GeminiProjectSuggestionGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_SendsStructuredRequestAndMapsAllSuggestionSections()
    {
        const string geminiResponse = """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      {
                        "text": "{\"contentSummary\":\"  Nội dung  \",\"strengthsSummary\":\"  Điểm mạnh  \",\"improvementSummary\":\"  Cần cải thiện  \"}"
                      }
                    ]
                  }
                }
              ]
            }
            """;
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(geminiResponse, Encoding.UTF8, "application/json")
        });
        using var httpClient = CreateHttpClient(handler);
        var generator = CreateGenerator(httpClient);

        var result = await generator.GenerateAsync(
            new ProjectSuggestionInput("CPMS", "Quản lý lịch review và nhận xét đồ án."),
            CancellationToken.None);

        Assert.Equal("Nội dung", result.ContentSummary);
        Assert.Equal("Điểm mạnh", result.StrengthsSummary);
        Assert.Equal("Cần cải thiện", result.ImprovementSummary);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal("test-api-key", handler.ApiKey);

        using var requestJson = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var root = requestJson.RootElement;
        Assert.Contains(
            "giảng viên",
            root.GetProperty("systemInstruction").GetProperty("parts")[0]
                .GetProperty("text").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(
            "application/json",
            root.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
        var required = root.GetProperty("generationConfig").GetProperty("responseSchema")
            .GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal(
            ["contentSummary", "strengthsSummary", "improvementSummary"],
            required);
    }

    [Fact]
    public async Task GenerateAsync_WhenApiKeyIsBlank_DoesNotCallGemini()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Must not call HTTP."));
        using var httpClient = CreateHttpClient(handler);
        var generator = CreateGenerator(httpClient, string.Empty);

        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() =>
            generator.GenerateAsync(
                new ProjectSuggestionInput("CPMS", "Project content"),
                CancellationToken.None));

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_WhenGeminiRateLimits_ReportsTemporaryUnavailability()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var httpClient = CreateHttpClient(handler);
        var generator = CreateGenerator(httpClient);

        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() =>
            generator.GenerateAsync(
                new ProjectSuggestionInput("CPMS", "Project content"),
                CancellationToken.None));
    }

    private static GeminiProjectSuggestionGenerator CreateGenerator(
        HttpClient httpClient,
        string apiKey = "test-api-key") =>
        new(
            httpClient,
            new GeminiOptions
            {
                ApiKey = apiKey,
                Model = "gemini-2.5-flash",
                TimeoutSeconds = 30
            },
            NullLogger<GeminiProjectSuggestionGenerator>.Instance);

    private static HttpClient CreateHttpClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"),
        Timeout = TimeSpan.FromSeconds(30)
    };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("x-goog-api-key").Single();
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
