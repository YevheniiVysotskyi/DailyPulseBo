# Daily Pulse Telegram Bot

ASP.NET Core 8.0 API + Telegram Bot that lets on-site staff record daily events and automatically produces an AI summary in Google Docs.

---

## ✨ Features

* One-tap Telegram UI for front-line staff (no extra apps).
* AI-generated daily report (GPT-4 by default) saved to Google Docs.
* Google Drive folder per client – all reports in one place.
* `/myid` command for instant administrator onboarding.
* Role-based access for owners / admins.
* EF Core + SQL Server storage.

---

## Prerequisites

| Tool / Service           | Version (tested) | Purpose                                |
|--------------------------|------------------|----------------------------------------|
| .NET SDK                 | 8.0.x            | Build & run the API                    |
| SQL Server               | 2022 (Express +) | Data storage (EF Core)                 |
| dotnet-ef CLI            | 8.0.x            | Create / apply EF migrations           |
| Google Cloud Project     | any              | Enable Drive & Docs APIs               |
| Telegram Bot Token       | —                | Bot messaging                          |
| OpenAI API Key           | GPT-4 / 3.5-turbo| Summarise daily events                 |

---

## Quick Start

1. **Configure `appsettings.json`**

   ```jsonc
   "ConnectionStrings": {
     "Default": "Server=.\\sqlexpress;..."
   },

   "Bot": {
     "Token":   "YOUR_TELEGRAM_BOT_TOKEN",
     "OwnerId": -1        // ← Creates automatically admin in the db so If u don't have ID yet put -1 or 0  
   },

   "OpenAI": {
     "ApiKey": "sk-XXXXXXXXXXXXXXXXXXXXXXXX",
     "Model":  "gpt-4"
   },

   "GoogleDrive": {
     "CredentialsPath": "Secrets/google.json",
     "FolderId": "FOLDER_ID"
   }

    "Prompts": {
        "DailyReport": [
            "...",
            "...",
        ]
    }
      jsonc```

---

2. **Restore packages & apply migrations**

dotnet restore
dotnet ef database update

---

3. **Run**

dotnet run

---

**Google Drive / Docs Setup**

1. Log in to https://console.cloud.google.com and create or select a project.
2. Menu → APIs & Services → Enabled APIs & services → Enable APIs & services.
  - Enable Google Drive API.
  - Enable Google Docs API (required for batch updates).
3. Menu → IAM & Admin → Service Accounts → Create a service account.
  - Name: for example. daily-pulse-bot.
  - Role: Project → Editor (or disk/document only roles - see note below).
4. Once created → Keys tab → Add key → JSON.
  - The daily-pulse-bot-xxxxxxxxxxxxxxxxxxxxxx.json file is downloaded.
5. Copy it to a safe location within the project (e.g. Secrets/google.json) or put it in any directory available to the container/host.
6. Specify the path: json "CredentialsPath": "Secrets/google.json".

1. Open drive.google.com → New folder (name it “Daily Pulse Reports”).
2. Open it, copy from the URL:
https://drive.google.com/drive/folders/⟨FOLDER_ID⟩ → everything after /folders/ is the line you need.
3. Insert: json "FolderId": "1wP0uV-abCq0eY7Y4JZQ-EXAMPLE".

1. Open the folder properties → Share / Share.
2. Leave the e-mail address of your service account (it ends with ...iam.gserviceaccount.com).
3. Role - Editor.

---

**Telegram Bot Setup & Ownership**

1. Bot API key is already present in the appsettings.json
2. Send the Telegram username of the person the bot should be transfered to to @isorianAI.

---

**OpenAI Account & API Key**

1. Go to https://platform.openai.com/ and create / log in to your account.

2. Billing → add a payment method and top-up balance.

3. API Keys → Create New Secret Key – copy it once.

4. Paste the key into OpenAI.ApiKey in appsettings.json.

5. Restart the backend (dotnet run) to apply changes.

---

**Editing the AI Prompt**

All prompt lines live in appsettings.json → Prompts.DailyReport.