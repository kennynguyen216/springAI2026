using Microsoft.Extensions.AI;

/// <summary>
/// Scans inbox content and creates calendar events from relevant email messages.
/// </summary>
public class InboxScannerService
{
    private readonly AppDbContext _dbContext;
    private readonly IChatClient _chatClient;
    private readonly SensitivityClassifier _sensitivityClassifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboxScannerService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="chatClient">The chat client used for structured extraction.</param>
    /// <param name="sensitivityClassifier">The classifier used to filter emails.</param>
    public InboxScannerService(
        AppDbContext dbContext,
        IChatClient chatClient,
        SensitivityClassifier sensitivityClassifier)
    {
        _dbContext = dbContext;
        _chatClient = chatClient;
        _sensitivityClassifier = sensitivityClassifier;
    }

    /// <summary>
    /// Scans the latest inbox messages and creates calendar events from valid results.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A summary of the scan result.</returns>
    public async Task<ScanInboxResponse> ScanAsync(CancellationToken cancellationToken = default)
    {
        var emailText = await EmailTool.ReadRecentEmails();
        if (emailText.StartsWith("I was able to connect", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Could not read emails.");
        }

        var emails = emailText.Split("---", StringSplitOptions.RemoveEmptyEntries);
        var relevantEmails = new List<string>();
        var blockedCount = 0;
        var irrelevantCount = 0;

        foreach (var email in emails)
        {
            var classification = await _sensitivityClassifier.ClassifyAsync(email.Trim());
            switch (classification)
            {
                case "relevant":
                    relevantEmails.Add(email.Trim());
                    break;
                case "sensitive":
                    blockedCount++;
                    Console.WriteLine("[CLASSIFIER] Blocked sensitive email.");
                    break;
                default:
                    irrelevantCount++;
                    Console.WriteLine("[CLASSIFIER] Skipped irrelevant email.");
                    break;
            }
        }

        if (relevantEmails.Count == 0)
        {
            var noRelevantMessage =
                $"No relevant emails found. ({irrelevantCount} irrelevant, {blockedCount} sensitive blocked.)";

            return new ScanInboxResponse(0, noRelevantMessage);
        }

        var extractionPrompt = BuildExtractionPrompt(relevantEmails);
        var response = await _chatClient.GetResponseAsync(extractionPrompt, cancellationToken: cancellationToken);
        var events = ParseEventPayload(response.Text ?? "[]");

        if (events is null || events.Count == 0)
        {
            return new ScanInboxResponse(0, "No important dates found in your inbox.");
        }

        var result = await SaveEventsAsync(events, irrelevantCount, blockedCount, cancellationToken);
        return result;
    }

    private static string BuildExtractionPrompt(IEnumerable<string> relevantEmails)
    {
        var joinedEmails = string.Join("\n\n---\n\n", relevantEmails);

        return $@"You are a date extraction assistant. Read the following emails and extract any important dates or events.
Return ONLY a valid JSON array with no extra text. Each item must have: ""title"", ""date"" (YYYY-MM-DD format), ""time"" (optional, e.g. '7PM'), ""description"".
If there are no important dates, return an empty array: []

Emails:
{joinedEmails}";
    }

    private static List<System.Text.Json.JsonElement>? ParseEventPayload(string rawJson)
    {
        var normalizedJson = rawJson.Trim();
        if (normalizedJson.Contains("```", StringComparison.Ordinal))
        {
            normalizedJson = System.Text.RegularExpressions.Regex
                .Match(normalizedJson, @"\[.*\]", System.Text.RegularExpressions.RegexOptions.Singleline)
                .Value;
        }

        return System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(normalizedJson);
    }

    private async Task<ScanInboxResponse> SaveEventsAsync(
        IEnumerable<System.Text.Json.JsonElement> events,
        int irrelevantCount,
        int blockedCount,
        CancellationToken cancellationToken)
    {
        var added = 0;
        var addedTitles = new List<string>();

        foreach (var calendarEvent in events)
        {
            var title = TryGetString(calendarEvent, "title");
            var dateString = TryGetString(calendarEvent, "date");
            var time = TryGetOptionalString(calendarEvent, "time");
            var description = TryGetString(calendarEvent, "description");

            if (string.IsNullOrWhiteSpace(title) || !DateTime.TryParse(dateString, out var parsedDate))
            {
                continue;
            }

            var finalDescription = string.IsNullOrWhiteSpace(time)
                ? description
                : $"{time}{(string.IsNullOrWhiteSpace(description) ? string.Empty : " — " + description)}";

            _dbContext.Events.Add(new CalendarEvent
            {
                Title = title,
                EventDate = parsedDate,
                Description = finalDescription
            });

            addedTitles.Add(
                $"{title} on {parsedDate:MMMM d, yyyy}{(string.IsNullOrWhiteSpace(time) ? string.Empty : " at " + time)}");
            added++;
        }

        if (added > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var summary = $" ({irrelevantCount} irrelevant skipped, {blockedCount} sensitive blocked.)";
        var message = added == 0
            ? $"No valid dates found.{summary}"
            : $"Added {added} event(s): {string.Join(", ", addedTitles)}{summary}";

        return new ScanInboxResponse(added, message);
    }

    private static string TryGetString(System.Text.Json.JsonElement jsonElement, string propertyName)
    {
        return jsonElement.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string? TryGetOptionalString(System.Text.Json.JsonElement jsonElement, string propertyName)
    {
        return jsonElement.TryGetProperty(propertyName, out var property)
            ? property.GetString()
            : null;
    }
}
