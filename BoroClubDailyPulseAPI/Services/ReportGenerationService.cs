using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;

public class ReportGenerationService : IReportGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _openAiOptions;
    private readonly PromptOptions _promptOptions;
    private readonly IGoogleDocsService _googleDocs;
    private readonly ILogger<ReportGenerationService> _logger;

    public ReportGenerationService(
        IHttpClientFactory httpClientFactory,
        IGoogleDocsService googleDocs,
        IOptions<OpenAiOptions> openAiOptions,
        IOptions<PromptOptions> promptOptions,
        ILogger<ReportGenerationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _openAiOptions = openAiOptions.Value;
        _promptOptions = promptOptions.Value;
        _googleDocs = googleDocs;
        _logger = logger;
    }

    public async Task<(string DocId, string DocUrl, string GptSummary, List<int> IncludedEventIds)> GenerateReportAsync(
        DateOnly date,
        List<Event> events,
        CancellationToken ct)
    {
        var eventsText = FormatEventsForGpt(events);
        var gptReport = await GenerateGptReportAsync(eventsText, ct);
        var (docId, docUrl) = await _googleDocs.CreateDailyReportAsync(date, eventsText, gptReport, ct);
        var includedEventIds = events.Select(e => e.Id).ToList();
        return (docId, docUrl, gptReport, includedEventIds);
    }

    private string FormatEventsForGpt(List<Event> events)
    {
        var sb = new StringBuilder();

        foreach (var e in events)
        {
            var status = e.IsCompleted ? "✅ Завершено" : "⌛ Потребує уваги завтра";

            sb.AppendLine(
                $"[{e.CreatedAt:HH:mm}] {e.UserName} " +
                $"({e.PropertyCategory} {e.PropertyName}): " +
                $"{e.EventType}. {e.Description}" +
                (string.IsNullOrEmpty(e.Tags) ? "" : $"  Теги: {e.Tags}") +
                $"  — {status}");
        }

        return sb.ToString();
    }

    private async Task<string> GenerateGptReportAsync(string eventsText, CancellationToken ct)
    {
        var promptText = string.Join("\n", _promptOptions.DailyReport);
        var prompt = promptText + "\n\nПодії дня:\n" + eventsText;

        var request = new
        {
            model = _openAiOptions.Model,
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant that creates daily reports in Ukrainian." },
                new { role = "user", content = prompt }
            },
            temperature = 0.7,
            max_tokens = 1000
        };

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openAiOptions.ApiKey);

        var response = await _httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/chat/completions",
            request,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OpenAI API error: {StatusCode}, Content: {ErrorContent}", response.StatusCode, errorContent);

            try
            {
                var errorJson = JsonDocument.Parse(errorContent);
                var error = errorJson.RootElement.GetProperty("error");
                var errorType = error.GetProperty("type").GetString();
                var errorCode = error.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;
                var errorMessage = error.GetProperty("message").GetString();

                bool isInsufficientFunds = false;
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests &&
                    errorMessage.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase))
                {
                    isInsufficientFunds = true;
                }
                else if (errorType == "insufficient_quota" ||
                         errorCode == "insufficient_quota" ||
                         errorMessage.Contains("billing", StringComparison.OrdinalIgnoreCase) ||
                         errorMessage.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                         errorMessage.Contains("limit", StringComparison.OrdinalIgnoreCase))
                {
                    isInsufficientFunds = true;
                }

                throw new OpenAiException(
                    $"OpenAI API error: {errorMessage}",
                    errorCode,
                    isInsufficientFunds);
            }
            catch (JsonException)
            {
                throw new OpenAiException($"OpenAI API error: {response.StatusCode}");
            }
        }

        var result = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        return result?.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "Порожній відгук від AI";
    }
}