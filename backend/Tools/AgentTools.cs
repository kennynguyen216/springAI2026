using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;

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
            var body = doc.MainDocumentPart?.Document.Body;
            return body?.InnerText ?? "The Word document is empty or unreadable.";
        }
        catch (Exception ex)
        {
            return $"Error reading Word doc: {ex.Message}";
        }
    }

    [Description("Connects to the user's inbox via IMAP to read the 5 most recent emails.")]
    public static async Task<string> ReadRecentEmails()
    {
        // Replace with your info or pull from AppSettings
        string email = "keithmills4444@gmail.com"; 
        string appPassword = "uhsstirksoiplnmh"; 

        try
        {
            using var client = new ImapClient();
            // Use imap.gmail.com for Gmail or outlook.office365.com for Outlook/LSU
            await client.ConnectAsync("imap.gmail.com", 993, true); 
            await client.AuthenticateAsync(email, appPassword);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            var results = new List<string>();
            int count = inbox.Count;
            int start = Math.Max(0, count - 5);

            for (int i = count - 1; i >= start; i--)
            {
                var message = await inbox.GetMessageAsync(i);

                string body = message.TextBody ?? "No content";
                string safeSnippet = body.Length > 300 ? body[..300] : body;

                results.Add($"From: {message.From}\nSubject: {message.Subject}\nContent: {safeSnippet}");
            }

            await client.DisconnectAsync(true);
            return string.Join("\n\n---\n\n", results);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GMAIL DEBUG ERROR]: {ex.Message}");
            return $"I was able to connect to the inbox, but I encountered a technical issue reading the message content. Please try again or check the terminal logs.";
        }
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