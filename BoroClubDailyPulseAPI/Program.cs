using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.Configure<BotOptions>(
    builder.Configuration.GetSection("Bot"));
builder.Services.Configure<OpenAiOptions>(
    builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<PromptOptions>(
        builder.Configuration.GetSection("Prompts"));

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(
        builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpClient();

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<BotOptions>>().Value;
    return new TelegramBotClient(opts.Token);
});

builder.Services.AddSingleton<IUserStateService, UserStateService>();
builder.Services.AddSingleton<IGoogleDocsService, GoogleDocsService>();
builder.Services.AddSingleton<IReportGenerationService, ReportGenerationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<UpdateHandler>();

builder.Services.AddHostedService<TelegramPollingService>();
builder.Services.AddHostedService<DailyReportService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

var botOpts = builder.Configuration.GetSection("Bot").Get<BotOptions>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (botOpts!.OwnerId != 0 && botOpts.OwnerId != -1)
    {
        if (!db.AdminUsers.Any(a => a.IsOwner))
        {
            db.AdminUsers.Add(new AdminUser
            {
                ChatId = botOpts.OwnerId,
                IsOwner = true
            });
            db.SaveChanges();
        }
    }
}

app.MapControllers();
app.Run();