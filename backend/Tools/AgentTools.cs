using System.ComponentModel;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;

public static class AgentTools
{
    private static readonly HttpClient _httpClient = new HttpClient();

    [Description("Gets the current time and current weather conditions for a specific city or location.")]
    public static async Task<string> GetWeatherAndTime(string location)
    {
        var currentTime = DateTime.Now.ToString("h:mm tt on dddd, MMMM d, yyyy");
        try
        {
            string formattedLocation = location.Replace(" ", "+");
            string url = $"https://wttr.in/{formattedLocation}?format=3";
            string weatherReport = await _httpClient.GetStringAsync(url);
            return $"System Time: {currentTime}. Live Weather Report: {weatherReport.Trim()}";
        }
        catch (Exception ex)
        {
            return $"System Time: {currentTime}. Weather data unavailable: {ex.Message}";
        }
    }

    [Description("Hands raw email text to the Email Agent to extract important dates and event details.")]
    public static async Task<string> AskEmailAgent(string emailBody, IServiceProvider sp, string threadId)
    {
        var agent = sp.GetRequiredKeyedService<AIAgent>("EmailAgent");
        var manager = sp.GetRequiredService<AgentSessionManager>();
        string sessionKey = $"{threadId}_email";
        if (!manager.Sessions.TryGetValue(sessionKey, out var session))
        {
            session = await agent.CreateSessionAsync();
            manager.Sessions[sessionKey] = session;
        }
        var result = await agent.RunAsync(emailBody, session);
        return result.Text ?? "No dates found.";
    }

    [Description("Sends structured event details to the Calendar Agent to be officially scheduled.")]
    public static async Task<string> AskCalendarAgent(string eventDetails, IServiceProvider sp, string threadId)
    {
        var agent = sp.GetRequiredKeyedService<AIAgent>("CalendarAgent");
        var manager = sp.GetRequiredService<AgentSessionManager>();
        string sessionKey = $"{threadId}_cal";
        if (!manager.Sessions.TryGetValue(sessionKey, out var session))
        {
            session = await agent.CreateSessionAsync();
            manager.Sessions[sessionKey] = session;
        }
        var result = await agent.RunAsync($"Schedule: {eventDetails}", session);
        return result.Text ?? "Event scheduled.";
    }

    [Description("Reads the text content from a PDF file.")]
    public static string ReadPdf(string filePath)
    {
        try
        {
            using var pdf = PdfDocument.Open(filePath);
            var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
            return string.IsNullOrWhiteSpace(text) ? "The PDF is empty." : text;
        }
        catch (Exception ex)
        {
            return $"Error reading PDF: {ex.Message}";
        }
    }

    [Description("Reads the text content from a Word (.docx) document.")]
    public static string ReadWord(string filePath)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            return body?.InnerText ?? "The Word document is empty or unreadable.";
        }
        catch (Exception ex)
        {
            return $"Error reading Word doc: {ex.Message}";
        }
    }

    [Description("Reads recent local sample emails from the configured sample directory.")]
    public static async Task<string> ReadRecentEmails(int maxResults, string? query, IServiceProvider sp)
    {
        var mailbox = sp.GetRequiredService<LocalMailboxService>();
        var messages = await mailbox.GetRecentMessagesAsync(
            Math.Clamp(maxResults, 1, 25),
            query,
            true);

        if (messages.Count == 0)
        {
            return "No local sample emails were found for that query.";
        }

        var builder = new StringBuilder();
        foreach (var message in messages)
        {
            var preview = message.PlainTextBody.Length > 240
                ? $"{message.PlainTextBody[..240]}..."
                : message.PlainTextBody;

            builder.AppendLine($"From: {message.FromAddress}");
            builder.AppendLine($"Subject: {message.Subject}");
            builder.AppendLine($"Received (UTC): {message.ReceivedAtUtc:yyyy-MM-dd HH:mm}");
            builder.AppendLine($"Preview: {preview}");
            builder.AppendLine();
            builder.AppendLine("---");
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    [Description("Runs the inbox manager workflow over local sample emails: classify, summarize, and add valid dates to the calendar.")]
    public static async Task<string> SyncAndOrganizeRecentEmails(
        int maxResults,
        string? query,
        bool applyLabels,
        bool addEventsToCalendar,
        IServiceProvider sp)
    {
        var manager = sp.GetRequiredService<EmailManagerAgentService>();
        var summary = await manager.ProcessInboxAsync(
            new EmailSyncRequest(
                Math.Clamp(maxResults, 1, 50),
                query,
                IncludeSpamTrash: true,
                ApplyLocalLabels: applyLabels,
                AddEventsToLocalCalendar: addEventsToCalendar,
                ForceReprocess: false));

        var builder = new StringBuilder();
        builder.AppendLine($"Processed: {summary.ProcessedCount}");
        builder.AppendLine($"Skipped (already indexed): {summary.SkippedCount}");
        builder.AppendLine($"Important: {summary.ImportantCount}");
        builder.AppendLine($"Promotions: {summary.PromotionsCount}");
        builder.AppendLine($"Spam: {summary.SpamCount}");
        builder.AppendLine($"Local calendar additions: {summary.EmailsWithLocalCalendarEvents}");

        foreach (var email in summary.Results.Where(result => !result.WasSkipped).Take(5))
        {
            builder.AppendLine();
            builder.AppendLine($"Subject: {email.Subject}");
            builder.AppendLine($"Category: {email.Category}");
            builder.AppendLine($"Summary: {email.Summary}");
            if (email.ActionItems.Count > 0)
            {
                builder.AppendLine($"Action items: {string.Join("; ", email.ActionItems)}");
            }
        }

        return builder.ToString().Trim();
    }

    [Description("Searches processed email summaries and embeddings using the local RAG index.")]
    public static async Task<string> SearchOrganizedEmails(string query, int top, IServiceProvider sp)
    {
        var manager = sp.GetRequiredService<EmailManagerAgentService>();
        var results = await manager.SearchInboxMemoryAsync(query, Math.Clamp(top, 1, 10));

        if (results.Count == 0)
        {
            return "No matching processed emails were found.";
        }

        return string.Join(
            "\n\n",
            results.Select(result =>
                $"[{result.Category}] {result.Subject}\nSimilarity: {result.Score:F2}\nSummary: {result.Summary}"));
    }

    [Description("Shows whether the local sample-email directory exists and where the workflow reads messages from.")]
    public static Task<string> GetLocalMailboxStatus(IServiceProvider sp)
    {
        var mailbox = sp.GetRequiredService<LocalMailboxService>();
        var directory = mailbox.ResolvedSampleDirectory;
        var lines = new[]
        {
            $"Sample directory exists: {Directory.Exists(directory)}",
            $"Sample directory: {directory}"
        };
        return Task.FromResult(string.Join("\n", lines));
    }

    [Description("Saves a new event to the user's personal calendar database.")]
    public static async Task<string> AddToCalendar(string title, string dateStr, string description, IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (DateTime.TryParse(dateStr, out DateTime parsedDate))
        {
            var newEvent = new CalendarEvent 
            { 
                Title = title, 
                EventDate = parsedDate, 
                Description = description 
            };
        
            db.Events.Add(newEvent);
            await db.SaveChangesAsync();
            return $"Successfully added '{title}' to your calendar for {parsedDate:MMMM dd, yyyy}.";
        }
    
        return "I couldn't parse that date. Please tell me the date in a clearer format (e.g., YYYY-MM-DD).";
    }
}
