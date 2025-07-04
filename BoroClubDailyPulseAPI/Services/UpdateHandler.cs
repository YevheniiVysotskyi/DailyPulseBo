using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class UpdateHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IUserStateService _stateService;
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly IAdminService _adminService;
    private readonly IReportGenerationService _reportGenerator;

    public UpdateHandler(
        ITelegramBotClient bot,
        IUserStateService stateService,
        IAdminService adminService,
        AppDbContext db,
        ILogger<UpdateHandler> logger,
        IReportGenerationService reportGenerator)
    {
        _bot = bot;
        _stateService = stateService;
        _adminService = adminService;
        _db = db;
        _logger = logger;
        _reportGenerator = reportGenerator;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        if (update.Type == UpdateType.Message && update.Message?.Text is { } text)
        {
            if (text.StartsWith("/"))
            {
                await HandleCommandsAsync(update.Message, text, ct);
                return;
            }

            await HandleMessageAsync(update.Message, ct);
            return;
        }

        if (update.Type == UpdateType.CallbackQuery)
        {
            await HandleCallbackQueryAsync(update.CallbackQuery!, ct);
        }
    }

    private async Task HandleCommandsAsync(Message msg, string cmd, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var isAdmin = await _adminService.IsAdminAsync(chatId);
        var adminUsr = await _db.AdminUsers.FirstOrDefaultAsync(a => a.ChatId == chatId);
        var isOwner = isAdmin && adminUsr is { IsOwner: true };

        switch (cmd.Split(' ').First())
        {
            case "/start":
                var state = _stateService.GetOrCreateState(chatId);
                state.CurrentStep = "main";
                await _bot.SendMessage(
                    chatId,
                    "Привіт! Що хочеш зробити?",
                    replyMarkup: Keyboards.Main,
                    cancellationToken: ct);
                break;

            case "/myid":
                await _bot.SendMessage(chatId, $"Ваш Chat ID: {chatId}", cancellationToken: ct);
                break;

            case "/admins":
                if (!isAdmin) return;
                var list = await _adminService.GetAllAsync();
                var txt = string.Join('\n', list.Select(a => $"• {a.ChatId} {(a.IsOwner ? "(Owner)" : "")}"));
                await _bot.SendMessage(chatId, $"Поточні адміни:\n{txt}");
                break;

            case "/admin_add":
                if (!isOwner) return;
                if (cmd.Split(' ').Length < 2) return;
                if (long.TryParse(cmd.Split(' ')[1], out var newId))
                {
                    await _adminService.AddAsync(newId);
                    await _bot.SendMessage(chatId, $"✅ Додано {newId} до списку адміністраторів.");
                }
                break;

            case "/admin_remove":
                if (!isOwner) return;
                if (cmd.Split(' ').Length < 2) return;
                if (long.TryParse(cmd.Split(' ')[1], out var delId))
                {
                    await _adminService.RemoveAsync(delId);
                    await _bot.SendMessage(chatId, $"✅ Видалено {delId} зі списку адміністраторів.");
                }
                break;
        }
    }

    private async Task HandleMessageAsync(Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var text = message.Text!;
        var state = _stateService.GetOrCreateState(chatId);

        switch (state.CurrentStep)
        {
            case "main":
                await HandleMainMenuAsync(message.From, chatId, text, state, ct);
                break;

            case "waiting_property_name":
                if (text == "⏩ Пропустити")
                {
                    state.CurrentEvent!.PropertyName = string.Empty;
                }
                else
                {
                    state.CurrentEvent!.PropertyName = text;
                }

                state.CurrentStep = "waiting_description";
                await _bot.SendMessage(
                    chatId,
                    "Опишіть ситуацію коротко (до 300 символів).\n💡 Підказка: \"Що сталося? Який результат? Чи є нюанси?\"",
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: ct);
                break;

            case "waiting_description":
                if (text.Length > 300)
                {
                    await _bot.SendMessage(
                        chatId,
                        "Опис занадто довгий. Будь ласка, скоротіть до 300 символів.",
                        cancellationToken: ct);
                    return;
                }

                state.CurrentEvent!.Description = text;
                state.CurrentStep = "waiting_tags";
                await _bot.SendMessage(
                    chatId,
                    "Бажаєте додати теги?\n(Можна обрати кілька або пропустити)",
                    replyMarkup: Keyboards.GetTagsKeyboard(state.SelectedTags),
                    cancellationToken: ct);
                break;

            default:
                if (state.CurrentStep != "main")
                {
                    await _bot.SendMessage(
                        chatId,
                        "Будь ласка, використовуйте кнопки вище для продовження.",
                        cancellationToken: ct);
                }
                break;
        }
    }

    private async Task HandleMainMenuAsync(User? user, long chatId, string text, UserState state, CancellationToken ct)
    {
        switch (text)
        {
            case "➕ Додати подію":
                state.CurrentEvent = new Event
                {
                    ChatId = chatId,
                    UserName = GetDisplayName(user)
                };
                state.CurrentStep = "waiting_event_type";
                await _bot.SendMessage(
                    chatId,
                    "Який тип події хочеш зафіксувати?",
                    replyMarkup: Keyboards.EventTypes,
                    cancellationToken: ct);
                break;

            case "📅 Переглянути звіт за день":
                if (!await _adminService.IsAdminAsync(chatId))
                {
                    await _bot.SendMessage(chatId, "⛔ У вас немає доступу до цієї функції.");
                    return;
                }

                await HandleDailyReportRequestAsync(chatId, ct);
                break;

            case "ℹ️ Що я можу?":
                await _bot.SendMessage(
                    chatId,
                    "🤖 Я допомагаю фіксувати щоденні події на об'єктах:\n\n" +
                    "• Заселення та виселення\n" +
                    "• Проблеми та їх вирішення\n" +
                    "• Прибирання об'єктів\n" +
                    "• Взаємодію з клієнтами\n" +
                    "• Завантаженість котеджів та бань\n\n" +
                    "Всі події автоматично збираються у звіт для керівництва.",
                    cancellationToken: ct);
                break;
        }
    }

    private async Task HandleDailyReportRequestAsync(long chatId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var todayEvents = await _db.Events
            .Where(e => e.CreatedAt.Date == DateTime.UtcNow.Date)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        if (!todayEvents.Any())
        {
            await _bot.SendMessage(
                chatId,
                "📅 Звіт за сьогодні:\n\n📝 Події поки не зафіксовані.\nКоли з'являться події, звіт буде доступний.",
                cancellationToken: ct);
            return;
        }

        var existingReport = await _db.DailyReports
            .FirstOrDefaultAsync(r => r.Date == today, ct);

        if (existingReport != null)
        {
            var todayEventIds = todayEvents.Select(e => e.Id).ToList();
            var missingEvents = todayEventIds.Except(existingReport.IncludedEvents).ToList();

            if (!missingEvents.Any())
            {
                await SendReportMessageSafely(chatId, existingReport, ct);
                return;
            }

            _logger.LogInformation("Deleting outdated report for {Date}. Missing events: {Count}", today, missingEvents.Count);
            _db.DailyReports.Remove(existingReport);
            await _db.SaveChangesAsync(ct);
        }

        await _bot.SendMessage(
            chatId,
            "📊 Генерую звіт на основі поточних подій...",
            cancellationToken: ct);

        DailyReport newReport = null;
        try
        {
            var (docId, docUrl, gptSummary, includedEventIds) =
                await _reportGenerator.GenerateReportAsync(today, todayEvents, ct);

            newReport = new DailyReport
            {
                Date = today,
                DocId = docId,
                DocUrl = docUrl,
                Summary = gptSummary,
                IncludedEvents = includedEventIds
            };

            _db.DailyReports.Add(newReport);
            await _db.SaveChangesAsync(ct);

            await SendReportMessageSafely(chatId, newReport, ct, isNewReport: true);
        }
        catch (OpenAiException ex) when (ex.IsInsufficientFunds)
        {
            _logger.LogError(ex, "OpenAI insufficient funds error");
            await _bot.SendMessage(
                chatId,
                "❌ Неможливо згенерувати AI-підсумок: недостатньо коштів на рахунку OpenAI.\n\n" +
                "⚠️ Зверніться до адміністратора для поповнення балансу.",
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during report generation process");

            if (newReport != null)
            {
                _logger.LogWarning("Report was created but error occurred afterwards. Attempting to send report link.");
                await SendReportMessageSafely(chatId, newReport, ct, isNewReport: true);
            }
            else
            {
                var reportCheck = await _db.DailyReports
                    .FirstOrDefaultAsync(r => r.Date == today, ct);

                if (reportCheck != null)
                {
                    _logger.LogWarning("Found report in DB despite exception. Sending to user.");
                    await SendReportMessageSafely(chatId, reportCheck, ct);
                }
                else
                {
                    await _bot.SendMessage(
                        chatId,
                        "❌ Помилка при генерації звіту. Спробуйте пізніше.",
                        cancellationToken: ct);
                }
            }
        }
    }

    private async Task SendReportMessageSafely(long chatId, DailyReport report, CancellationToken ct, bool isNewReport = false)
    {
        try
        {
            var msg = isNewReport
                ? $"✅ Звіт за {report.Date:dd.MM.yyyy} (згенеровано зараз):\n📄 {report.DocUrl}\n\n🤖 AI-підсумок:\n{report.Summary}"
                : $"📄 Звіт за {report.Date:dd.MM.yyyy}:\n{report.DocUrl}\n\n🤖 AI-підсумок:\n{report.Summary}";

            await _bot.SendMessage(chatId, msg, cancellationToken: ct);
        }
        catch (Exception sendEx)
        {
            _logger.LogError(sendEx, "Failed to send report message with markdown. Retrying without markdown.");

            try
            {
                var fallback = isNewReport
                    ? $"✅ Звіт за {report.Date:dd.MM.yyyy} (згенеровано зараз):\n📄 {report.DocUrl}\n\nAI-підсумок:\n{report.Summary}"
                    : $"📄 Звіт за {report.Date:dd.MM.yyyy}:\n{report.DocUrl}\n\nAI-підсумок:\n{report.Summary}";

                await _bot.SendMessage(chatId, fallback, cancellationToken: ct);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Failed to send report message even without markdown. Sending minimal message.");

                try
                {
                    await _bot.SendMessage(chatId, $"✅ Звіт готовий: {report.DocUrl}", cancellationToken: ct);
                }
                catch (Exception minimalEx)
                {
                    _logger.LogError(minimalEx, "Failed to send even minimal message. User will need to request report again.");
                }
            }
        }
    }

    private static string GetDisplayName(User? from) =>
        !string.IsNullOrWhiteSpace(from?.Username)
            ? $"@{from!.Username}"
            : $"{from?.FirstName} {from?.LastName}".Trim();

    private async Task HandleCallbackQueryAsync(CallbackQuery query, CancellationToken ct)
    {
        var chatId = query.Message!.Chat.Id;
        var data = query.Data!;
        var state = _stateService.GetOrCreateState(chatId);

        if (data.StartsWith("event_"))
        {
            await _bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);

            var eventType = data.Replace("event_", "");
            state.CurrentEvent!.EventType = GetEventTypeDisplay(eventType);
            state.CurrentStep = "waiting_property_category";

            await _bot.EditMessageText(
                chatId,
                query.Message.MessageId,
                "Оберіть категорію об'єкта:",
                replyMarkup: Keyboards.PropertyCategories,
                cancellationToken: ct);
        }
        else if (data.StartsWith("prop_"))
        {
            await _bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);

            var propCategory = data.Replace("prop_", "");
            state.CurrentEvent!.PropertyCategory = GetPropertyCategoryDisplay(propCategory);
            state.CurrentStep = "waiting_property_name";

            await _bot.EditMessageText(
                chatId,
                query.Message.MessageId,
                "Введіть точну назву або номер об'єкта (наприклад: Котедж №2, Баня №3):\n" +
                "Або натисніть кнопку «⏩ Пропустити» нижче.",
                cancellationToken: ct);

            await _bot.SendMessage(
                chatId,
                "⏩ Ви можете пропустити цей крок:",
                replyMarkup: Keyboards.SkipPropertyName,
                cancellationToken: ct);
        }
        else if (data.StartsWith("tag_"))
        {
            await HandleTagSelectionAsync(chatId, query.Message!.MessageId, data, state, query.Id, ct);
        }
        else if (data.StartsWith("status_"))
        {
            await _bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
            await HandleStatusSelectionAsync(chatId, query.Message!.MessageId, data, state, ct);
        }
    }

    private async Task HandleTagSelectionAsync(
        long chatId,
        int messageId,
        string data,
        UserState state,
        string callbackId,
        CancellationToken ct)
    {
        if (data == "tag_skip")
        {
            state.SelectedTags.Clear();
            await _bot.AnswerCallbackQuery(callbackId, "Пропускаємо теги", cancellationToken: ct);

            state.CurrentStep = "waiting_status";
            await _bot.EditMessageText(
                chatId,
                messageId,
                "Це завершена дія чи потребує продовження?",
                replyMarkup: Keyboards.CompletionStatus,
                cancellationToken: ct);
        }
        else if (data == "tag_continue")
        {
            await _bot.AnswerCallbackQuery(callbackId, $"Обрано тегів: {state.SelectedTags.Count}", cancellationToken: ct);

            state.CurrentStep = "waiting_status";
            await _bot.EditMessageText(
                chatId,
                messageId,
                $"Обрані теги: {string.Join(", ", state.SelectedTags)}\n\nЦе завершена дія чи потребує продовження?",
                replyMarkup: Keyboards.CompletionStatus,
                cancellationToken: ct);
        }
        else
        {
            var tag = GetTagDisplay(data.Replace("tag_", ""));
            if (state.SelectedTags.Contains(tag))
            {
                state.SelectedTags.Remove(tag);
                await _bot.AnswerCallbackQuery(callbackId, $"❌ Знято: {tag}", cancellationToken: ct);
            }
            else
            {
                state.SelectedTags.Add(tag);
                await _bot.AnswerCallbackQuery(callbackId, $"✅ Додано: {tag}", cancellationToken: ct);
            }

            await _bot.EditMessageReplyMarkup(
                chatId,
                messageId,
                replyMarkup: Keyboards.GetTagsKeyboard(state.SelectedTags),
                cancellationToken: ct);
        }
    }

    private async Task HandleStatusSelectionAsync(
        long chatId,
        int messageId,
        string data,
        UserState state,
        CancellationToken ct)
    {
        state.CurrentEvent!.IsCompleted = data == "status_completed";
        state.CurrentEvent!.Tags = string.Join(", ", state.SelectedTags);

        _db.Events.Add(state.CurrentEvent);
        await _db.SaveChangesAsync(ct);

        await _bot.EditMessageText(
            chatId,
            messageId,
            "✅ Подія збережена!\nВона увійде до звіту за день.",
            cancellationToken: ct);

        await _bot.SendMessage(
            chatId,
            "Що далі?",
            replyMarkup: Keyboards.Main,
            cancellationToken: ct);

        _stateService.RemoveState(chatId);
    }

    private string GetEventTypeDisplay(string t) => t switch
    {
        "checkin" => "Заселення / Виселення",
        "problem" => "Проблема / факап",
        "solved" => "Вирішена проблема",
        "cleaning" => "Прибирання / підготовка",
        "client" => "Спілкування з клієнтом",
        "occupancy" => "Завантаженість / простій",
        "other" => "Інше",
        _ => t
    };

    private string GetPropertyCategoryDisplay(string c) => c switch
    {
        "standard" => "Стандартний котедж",
        "improved" => "Покращений котедж",
        "vip" => "ВІП котедж",
        "themed" => "Тематичний котедж",
        "sauna" => "Баня",
        "terrace" => "Тераса",
        "other" => "Інше",
        _ => c
    };

    private string GetTagDisplay(string t) => t switch
    {
        "cleaned" => "#прибрано",
        "broken" => "#поломка",
        "fixed" => "#виправлено",
        "fuckup" => "#факап",
        "recurring" => "#повторнапроблема",
        "idle" => "#простій",
        "unhappy" => "#клієнтнезадоволений",
        "satisfied" => "#клієнтзадоволений",
        "welldone" => "#успішновирішено",
        "check" => "#рекомендуюперевірити",
        _ => t
    };
}
