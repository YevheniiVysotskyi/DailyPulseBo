using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

public class GoogleDocsService : IGoogleDocsService
{
    private readonly DriveService _drive;
    private readonly DocsService _docs;
    private readonly string _folderId;

    public GoogleDocsService(IConfiguration cfg)
    {
        var credPath = cfg["GoogleDrive:CredentialsPath"];
        _folderId = cfg["GoogleDrive:FolderId"] ?? throw new ArgumentNullException("FolderId");

        GoogleCredential credential =
            GoogleCredential.FromFile(credPath)
                            .CreateScoped(DriveService.ScopeConstants.Drive,
                                          DocsService.ScopeConstants.Documents);

        _drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DailyPulse"
        });

        _docs = new DocsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DailyPulse"
        });
      }

    public async Task<(string docId, string docUrl)> CreateDailyReportAsync(
        DateOnly date, string eventsText, string gptSummary, CancellationToken ct = default)
    {
        var fileMeta = new Google.Apis.Drive.v3.Data.File
        {
            Name = $"Звіт_{date:dd-MM-yyyy}",
            MimeType = "application/vnd.google-apps.document",
            Parents = new[] { _folderId }
        };

        var createRq = _drive.Files.Create(fileMeta);
        createRq.Fields = "id, webViewLink";
        var file = await createRq.ExecuteAsync(ct);

        var reqs = new List<Request>
        {
            new Request
            {
                InsertText = new InsertTextRequest
                {
                    Text       = $"📅 Звіт за {date:dd.MM.yyyy}\n\nПодії дня:\n{eventsText}\n\n— — —\n\n{gptSummary}",
                    Location   = new Google.Apis.Docs.v1.Data.Location { Index = 1 }
                }
            }
        };

        await _docs.Documents.BatchUpdate(new BatchUpdateDocumentRequest
        {
            Requests = reqs
        }, file.Id).ExecuteAsync(ct);

        return (file.Id!, file.WebViewLink!);
    }
}
