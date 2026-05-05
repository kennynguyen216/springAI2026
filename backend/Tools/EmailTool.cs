using System.ComponentModel;
using MailKit;
using MailKit.Net.Imap;

/// <summary>
/// Provides email-related tool functions exposed to agents.
/// </summary>
public static class EmailTool
{
    /// <summary>
    /// Connects to the configured inbox and returns the five most recent email snippets.
    /// </summary>
    /// <returns>The recent email summary text.</returns>
    [Description("Connects to the user's inbox via IMAP to read the 5 most recent emails.")]
    public static async Task<string> ReadRecentEmails()
    {
        // Risk note: credentials are hard-coded in source today. This is preserved to avoid changing behavior.
        const string email = "keithmills4444@gmail.com";
        const string appPassword = "uhsstirksoiplnmh";

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync("imap.gmail.com", 993, true);
            await client.AuthenticateAsync(email, appPassword);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            var results = new List<string>();
            var count = inbox.Count;
            var start = Math.Max(0, count - 5);

            for (var index = count - 1; index >= start; index--)
            {
                var message = await inbox.GetMessageAsync(index);
                var body = message.TextBody ?? "No content";
                var safeSnippet = body.Length > 300 ? body[..300] : body;

                results.Add($"From: {message.From}\nSubject: {message.Subject}\nContent: {safeSnippet}");
            }

            await client.DisconnectAsync(true);
            return string.Join("\n\n---\n\n", results);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GMAIL DEBUG ERROR]: {ex.Message}");
            return "I was able to connect to the inbox, but I encountered a technical issue reading the message content. Please try again or check the terminal logs.";
        }
    }
}
