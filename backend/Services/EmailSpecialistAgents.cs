using System.Text.Json;
using Microsoft.Extensions.AI;

public sealed class StructuredAgentRunner
{
    private readonly IChatClient _chatClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public StructuredAgentRunner(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<T> RunAsync<T>(
        string systemPrompt,
        string userPrompt,
        string schemaName,
        string schemaDescription,
        CancellationToken cancellationToken = default)
    {
        var response = await _chatClient.GetResponseAsync(
            new List<Microsoft.Extensions.AI.ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            },
            new ChatOptions
            {
                Temperature = 0f,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<T>(_jsonOptions, schemaName, schemaDescription)
            },
            cancellationToken);

        return Deserialize<T>(response.Text);
    }

    private T Deserialize<T>(string rawText)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(rawText, _jsonOptions)
                ?? throw new InvalidOperationException("The model returned an empty structured response.");
        }
        catch
        {
            var json = ExtractJson(rawText);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions)
                ?? throw new InvalidOperationException("The model returned JSON that could not be read.");
        }
    }

    private static string ExtractJson(string text)
    {
        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return text[firstBrace..(lastBrace + 1)];
        }

        throw new InvalidOperationException("The model response did not contain a JSON object.");
    }
}

public sealed class EmailClassificationAgent
{
    private readonly StructuredAgentRunner _runner;

    public EmailClassificationAgent(StructuredAgentRunner runner)
    {
        _runner = runner;
    }

    public Task<EmailClassificationDecision> ClassifyAsync(
        InboxEmailMessage email,
        IReadOnlyList<ProcessedEmailSearchHit> relatedEmails,
        CancellationToken cancellationToken = default)
    {
        var relatedContext = relatedEmails.Count == 0
            ? "No related email memory was found."
            : string.Join(
                "\n\n",
                relatedEmails.Select(hit =>
                    $"Similarity: {hit.Score:F2}\nStored Category: {hit.Category}\nSubject: {hit.Subject}\nSummary: {hit.Summary}"));

        return _runner.RunAsync<EmailClassificationDecision>(
            systemPrompt:
                """
                You are the Classification Agent in an inbox manager workflow.
                Classify the email into exactly one category:
                - Important
                - Promotions
                - Spam
                - General

                Rules:
                - Use Important for deadlines, exams, project work, professor updates, billing problems, urgent logistics, or action-required messages.
                - Use Promotions for marketing, sales, newsletters, and discounts.
                - Use Spam for scams, phishing, fake invoices, suspicious urgency, or obvious junk.
                - Use General when it is neither important, promotional, nor spam.
                - Be conservative with Spam classification.
                """,
            userPrompt:
                $"""
                Related inbox memory:
                {relatedContext}

                Email to classify:
                {email.ToContextBlock()}
                """,
            schemaName: "email_classification_decision",
            schemaDescription: "A category decision with a short rationale and importance score.",
            cancellationToken);
    }
}

public sealed class EmailSummaryAgent
{
    private readonly StructuredAgentRunner _runner;

    public EmailSummaryAgent(StructuredAgentRunner runner)
    {
        _runner = runner;
    }

    public Task<EmailSummaryResult> SummarizeAsync(
        InboxEmailMessage email,
        EmailClassificationDecision classification,
        IReadOnlyList<ProcessedEmailSearchHit> relatedEmails,
        CancellationToken cancellationToken = default)
    {
        var relatedContext = relatedEmails.Count == 0
            ? "No related email memory was found."
            : string.Join(
                "\n\n",
                relatedEmails.Select(hit =>
                    $"Subject: {hit.Subject}\nSummary: {hit.Summary}"));

        return _runner.RunAsync<EmailSummaryResult>(
            systemPrompt:
                """
                You are the Summary Agent in an inbox manager workflow.
                Produce a quick email summary so a student can understand the message without opening it.

                Rules:
                - Keep the one-line summary under 25 words.
                - Keep the detailed summary under 80 words.
                - Extract concise action items.
                - Mark calendarCandidate true only if the email contains a concrete date, deadline, exam, due date, meeting, or event.
                """,
            userPrompt:
                $"""
                Existing related memory:
                {relatedContext}

                Classification already chosen:
                Category: {classification.Category}
                Reasoning: {classification.Reasoning}

                Email:
                {email.ToContextBlock()}
                """,
            schemaName: "email_summary_result",
            schemaDescription: "A compact summary, action items, and calendar signal for an email.",
            cancellationToken);
    }
}

public sealed class EmailEventExtractionAgent
{
    private readonly StructuredAgentRunner _runner;

    public EmailEventExtractionAgent(StructuredAgentRunner runner)
    {
        _runner = runner;
    }

    public Task<EmailEventExtractionResult> ExtractAsync(
        InboxEmailMessage email,
        EmailSummaryResult summary,
        CancellationToken cancellationToken = default)
    {
        return _runner.RunAsync<EmailEventExtractionResult>(
            systemPrompt:
                """
                You are the Event Extraction Agent in an inbox manager workflow.
                Extract dates and schedule-worthy events from the email.

                Rules:
                - Only return events that are explicitly supported by the email.
                - Use allDay = true when a date exists but a time does not.
                - Leave date or times empty if they are not present.
                - Confidence must be between 0 and 1.
                """,
            userPrompt:
                $"""
                Email summary:
                {summary.DetailedSummary}

                Action items:
                {string.Join("; ", summary.ActionItems)}

                Email:
                {email.ToContextBlock()}
                """,
            schemaName: "email_event_extraction_result",
            schemaDescription: "Possible events and dates detected in the email.",
            cancellationToken);
    }
}

public sealed class CalendarValidationAgent
{
    private readonly StructuredAgentRunner _runner;

    public CalendarValidationAgent(StructuredAgentRunner runner)
    {
        _runner = runner;
    }

    public Task<CalendarValidationResult> ValidateAsync(
        EmailEventExtractionResult extractedEvents,
        CancellationToken cancellationToken = default)
    {
        return _runner.RunAsync<CalendarValidationResult>(
            systemPrompt:
                """
                You are the Calendar Agent in an inbox manager workflow.
                Validate extracted events before they are added to a calendar.

                Rules:
                - Only approve events with enough detail to be useful.
                - Approve all-day events if they contain a clear date and title.
                - Reject vague or duplicate-looking items.
                - Provide a short reason for each decision.
                """,
            userPrompt:
                $"""
                Extracted event candidates:
                {JsonSerializer.Serialize(extractedEvents)}
                """,
            schemaName: "calendar_validation_result",
            schemaDescription: "Validated calendar events with approval decisions.",
            cancellationToken);
    }
}

public sealed record EmailClassificationDecision(
    string Category,
    string Reasoning,
    int ImportanceScore);

public sealed record EmailSummaryResult(
    string OneLineSummary,
    string DetailedSummary,
    List<string> ActionItems,
    bool CalendarCandidate);

public sealed record EmailEventExtractionResult(
    List<EmailEventCandidate> Events);

public sealed record EmailEventCandidate(
    string Title,
    string Date,
    string StartTime,
    string EndTime,
    bool AllDay,
    double Confidence,
    string Description);

public sealed record CalendarValidationResult(
    List<ValidatedCalendarEvent> Events);

public sealed record ValidatedCalendarEvent(
    string Title,
    string Date,
    string StartTime,
    string EndTime,
    bool AllDay,
    double Confidence,
    bool ShouldAdd,
    string Reason,
    string Description);
