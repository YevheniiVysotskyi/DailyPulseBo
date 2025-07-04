public interface IGoogleDocsService
{
    Task<(string docId, string docUrl)> CreateDailyReportAsync(
        DateOnly date,
        string eventsText,
        string gptSummary,
        CancellationToken ct = default);
}
