using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Generic;

public static class Keyboards
{
    public static ReplyKeyboardMarkup Main => new(new[]
    {
        new[] { new KeyboardButton("➕ Додати подію") },
        new[] { new KeyboardButton("📅 Переглянути звіт за день") },
        new[] { new KeyboardButton("ℹ️ Що я можу?") }
    })
    {
        ResizeKeyboard = true,
        OneTimeKeyboard = false
    };

    public static ReplyKeyboardMarkup SkipPropertyName => new(new[]
    {
        new[] { new KeyboardButton("⏩ Пропустити") }
    })
    {
        ResizeKeyboard = true,
        OneTimeKeyboard = true
    };

    public static InlineKeyboardMarkup EventTypes => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🏠 Заселення / Виселення", "event_checkin") },
        new[] { InlineKeyboardButton.WithCallbackData("⚠️ Проблема / факап", "event_problem") },
        new[] { InlineKeyboardButton.WithCallbackData("🔧 Вирішена проблема", "event_solved") },
        new[] { InlineKeyboardButton.WithCallbackData("🧽 Прибирання / підготовка", "event_cleaning") },
        new[] { InlineKeyboardButton.WithCallbackData("🤝 Спілкування з клієнтом", "event_client") },
        new[] { InlineKeyboardButton.WithCallbackData("📈 Завантаженість / простій", "event_occupancy") },
        new[] { InlineKeyboardButton.WithCallbackData("💬 Інше", "event_other") }
    });

    public static InlineKeyboardMarkup PropertyCategories => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🏕 Стандартний котедж", "prop_standard") },
        new[] { InlineKeyboardButton.WithCallbackData("✨ Покращений котедж", "prop_improved") },
        new[] { InlineKeyboardButton.WithCallbackData("💎 ВІП котедж", "prop_vip") },
        new[] { InlineKeyboardButton.WithCallbackData("🎭 Тематичний котедж", "prop_themed") },
        new[] { InlineKeyboardButton.WithCallbackData("🔥 Баня", "prop_sauna") },
        new[] { InlineKeyboardButton.WithCallbackData("🌿 Тераса", "prop_terrace") },
        new[] { InlineKeyboardButton.WithCallbackData("🏢 Інше", "prop_other") }
    });

    public static InlineKeyboardMarkup GetTagsKeyboard(List<string> selectedTags)
    {
        var buttons = new List<InlineKeyboardButton[]>
        {
            new[]
            {
                CreateTagButton("#прибрано", "tag_cleaned", selectedTags),
                CreateTagButton("#поломка", "tag_broken", selectedTags)
            },
            new[]
            {
                CreateTagButton("#виправлено", "tag_fixed", selectedTags),
                CreateTagButton("#факап", "tag_fuckup", selectedTags)
            },
            new[]
            {
                CreateTagButton("#повторнапроблема", "tag_recurring", selectedTags),
                CreateTagButton("#клієнтзадоволений", "tag_satisfied", selectedTags)
            },
            new[]
            {
                CreateTagButton("#простій", "tag_idle", selectedTags),
                CreateTagButton("#клієнтнезадоволений", "tag_unhappy", selectedTags)
            },
            new[]
            {
                CreateTagButton("#успішновирішено", "tag_welldone", selectedTags),
                CreateTagButton("#рекомендуюперевірити", "tag_check", selectedTags)
            }
        };

        if (selectedTags.Count > 0)
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    $"✅ Продовжити ({selectedTags.Count} тегів)", "tag_continue")
            });
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("⏩ Пропустити без тегів", "tag_skip")
            });
        }
        else
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("⏩ Продовжити без тегів", "tag_skip")
            });
        }

        return new InlineKeyboardMarkup(buttons);
    }

    private static InlineKeyboardButton CreateTagButton(string display, string cbData, List<string> selectedTags)
    {
        var selected = selectedTags.Contains(display);
        var title = selected ? $"✓ {display}" : display;
        return InlineKeyboardButton.WithCallbackData(title, cbData);
    }

    public static InlineKeyboardMarkup CompletionStatus => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("✅ Завершено", "status_completed"),
            InlineKeyboardButton.WithCallbackData("⏳ Потребує уваги завтра", "status_pending")
        }
    });
}
