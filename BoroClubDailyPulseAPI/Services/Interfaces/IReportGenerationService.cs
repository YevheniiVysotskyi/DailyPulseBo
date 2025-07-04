public interface IReportGenerationService
{
    Task<(string DocId, string DocUrl, string GptSummary, List<int> IncludedEventIds)> GenerateReportAsync(
        DateOnly date,
        List<Event> events,
        CancellationToken ct);
}