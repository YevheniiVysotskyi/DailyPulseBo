using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

public class DailyReportService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyReportService> _logger;

    public DailyReportService(
        IServiceProvider serviceProvider,
        ILogger<DailyReportService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddDays(1).AddHours(23).AddMinutes(59);

            if (now.Hour == 23 && now.Minute == 59)
            {
                await GenerateDailyReportAsync(stoppingToken);
                nextRun = now.Date.AddDays(1).AddHours(23).AddMinutes(59);
            }

            var delay = nextRun - now;
            _logger.LogInformation("Next report generation at {NextRun}", nextRun);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task GenerateDailyReportAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var reportGenerator = scope.ServiceProvider.GetRequiredService<IReportGenerationService>();
            var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
            var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();

            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var events = await db.Events
                .Where(e => e.CreatedAt.Date == DateTime.UtcNow.Date)
                .OrderBy(e => e.CreatedAt)
                .ToListAsync(ct);

            if (!events.Any())
            {
                _logger.LogInformation("No events for {Date}", today);
                return;
            }

            var existingReport = await db.DailyReports
                .FirstOrDefaultAsync(r => r.Date == today, ct);

            if (existingReport != null)
            {
                var todayEventIds = events.Select(e => e.Id).ToList();
                var missingEvents = todayEventIds.Except(existingReport.IncludedEvents).ToList();

                if (!missingEvents.Any())
                {
                    _logger.LogInformation("Report for {Date} already exists and includes all {EventCount} events", today, events.Count);
                    return;
                }
                else
                {
                    _logger.LogInformation("Deleting outdated report for {Date}. Missing events: {Count}", today, missingEvents.Count);
                    db.DailyReports.Remove(existingReport);
                    await db.SaveChangesAsync(ct);
                }
            }

            var (docId, docUrl, gptSummary, includedEventIds) = await reportGenerator.GenerateReportAsync(today, events, ct);

            db.DailyReports.Add(new DailyReport
            {
                Date = today,
                DocId = docId,
                DocUrl = docUrl,
                Summary = gptSummary,
                IncludedEvents = includedEventIds
            });

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Generated daily report for {Date} with {EventCount} events", today, includedEventIds.Count);
        }
        catch (OpenAiException ex) when (ex.IsInsufficientFunds)
        {
            _logger.LogError(ex, "OpenAI insufficient funds error during scheduled report generation");

            using var scope = _serviceProvider.CreateScope();
            var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();
            var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

            var admins = await adminService.GetAllAsync();
            foreach (var admin in admins)
            {
                try
                {
                    await bot.SendMessage(
                        admin.ChatId,
                        "⚠️ УВАГА: Автоматичний звіт не згенеровано!\n\n" +
                        "❌ Причина: недостатньо коштів на рахунку OpenAI.\n" +
                        "📅 Дата: " + DateOnly.FromDateTime(DateTime.UtcNow.Date).ToString("dd.MM.yyyy") + "\n\n" +
                        "Необхідно терміново поповнити баланс OpenAI.",
                        cancellationToken: ct);
                }
                catch (Exception notifyEx)
                {
                    _logger.LogError(notifyEx, "Failed to notify admin {ChatId} about insufficient funds", admin.ChatId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating daily report");
        }
    }
}