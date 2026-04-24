using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class EmailManagerAgentService
{
    private readonly GmailMailboxService _gmailMailbox;
    private readonly EmailMemoryService _memory;
    private readonly EmailClassificationAgent _classificationAgent;
    private readonly EmailSummaryAgent _summaryAgent;
    private readonly EmailEventExtractionAgent _eventExtractionAgent;
    private readonly CalendarValidationAgent _calendarValidationAgent;
    private readonly AppDbContext _dbContext;
    private readonly EmailProcessingOptions _options;

    public EmailManagerAgentService(
        GmailMailboxService gmailMailbox,
        EmailMemoryService memory,
        EmailClassificationAgent classificationAgent,
        EmailSummaryAgent summaryAgent,
        EmailEventExtractionAgent eventExtractionAgent,
        CalendarValidationAgent calendarValidationAgent,
        AppDbContext dbContext,
        IOptions<EmailProcessingOptions> options)
    {
        _gmailMailbox = gmailMailbox;
        _memory = memory;
        _classificationAgent = classificationAgent;
        _summaryAgent = summaryAgent;
        _eventExtractionAgent = eventExtractionAgent;
        _calendarValidationAgent = calendarValidationAgent;
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<EmailSyncSummary> ProcessInboxAsync(EmailSyncRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new EmailSyncRequest(
            _options.DefaultSyncCount,
            _options.DefaultQuery,
            true,
            _options.ApplyGmailLabelsByDefault,
            _options.AddEventsToLocalCalendarByDefault,
            _options.AddEventsToGoogleCalendarByDefault,
            false);

        var messages = await _gmailMailbox.GetRecentMessagesAsync(
            request.MaxResults,
            request.Query,
            request.IncludeSpamTrash,
            cancellationToken);

        var processed = new List<EmailProcessingResult>();
        foreach (var message in messages)
        {
            var result = await ProcessSingleEmailAsync(message, request, cancellationToken);
            processed.Add(result);
        }

        return new EmailSyncSummary(
            processed.Count,
            processed.Count(result => result.WasSkipped),
            processed.Count(result => !result.WasSkipped),
            processed.Count(result => result.Category == EmailCategory.Important.ToString()),
            processed.Count(result => result.Category == EmailCategory.Promotions.ToString()),
            processed.Count(result => result.Category == EmailCategory.Spam.ToString()),
            processed.Count(result => result.EventsAddedToLocalCalendar),
            processed.Count(result => result.EventsAddedToGoogleCalendar),
            processed);
    }

    public Task<List<ProcessedEmailSearchHit>> SearchInboxMemoryAsync(string query, int top = 5, CancellationToken cancellationToken = default)
    {
        return _memory.SearchAsync(query, top, cancellationToken);
    }

    public async Task<List<CalendarEvent>> AddEventsForEmailAsync(int processedEmailId, bool syncToGoogleCalendar, CancellationToken cancellationToken = default)
    {
        var email = await _dbContext.ProcessedEmails
            .Include(item => item.CalendarSuggestions)
            .FirstOrDefaultAsync(item => item.Id == processedEmailId, cancellationToken);

        if (email is null)
        {
            throw new InvalidOperationException($"No processed email was found for id '{processedEmailId}'.");
        }

        var created = new List<CalendarEvent>();
        foreach (var suggestion in email.CalendarSuggestions.Where(item => item.StartUtc.HasValue))
        {
            if (!suggestion.AddedToLocalCalendar)
            {
                var localCalendarEvent = new CalendarEvent
                {
                    Title = suggestion.Title,
                    EventDate = suggestion.StartUtc!.Value,
                    Description = suggestion.Description
                };

                _dbContext.Events.Add(localCalendarEvent);
                suggestion.AddedToLocalCalendar = true;
                created.Add(localCalendarEvent);
            }

            if (syncToGoogleCalendar && !suggestion.AddedToGoogleCalendar)
            {
                var googleEventId = await _gmailMailbox.CreateGoogleCalendarEventAsync(suggestion, cancellationToken);
                suggestion.AddedToGoogleCalendar = !string.IsNullOrWhiteSpace(googleEventId);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    private async Task<EmailProcessingResult> ProcessSingleEmailAsync(
        InboxEmailMessage message,
        EmailSyncRequest request,
        CancellationToken cancellationToken)
    {
        var alreadyProcessed = await _dbContext.ProcessedEmails
            .AsNoTracking()
            .FirstOrDefaultAsync(email => email.GmailMessageId == message.GmailMessageId, cancellationToken);

        if (alreadyProcessed is not null && !request.ForceReprocess)
        {
            return new EmailProcessingResult(
                message.GmailMessageId,
                message.Subject,
                alreadyProcessed.Category,
                alreadyProcessed.Summary,
                Array.Empty<string>(),
                Array.Empty<CalendarEventSummary>(),
                true,
                false,
                false);
        }

        var memoryContext = await _memory.SearchAsync(
            $"{message.Subject}\n{message.Snippet}",
            top: 3,
            cancellationToken);

        var classification = await _classificationAgent.ClassifyAsync(message, memoryContext, cancellationToken);
        var parsedCategory = Enum.TryParse<EmailCategory>(classification.Category, ignoreCase: true, out var category)
            ? category
            : EmailCategory.General;

        var summary = await _summaryAgent.SummarizeAsync(message, classification, memoryContext, cancellationToken);
        var extracted = summary.CalendarCandidate
            ? await _eventExtractionAgent.ExtractAsync(message, summary, cancellationToken)
            : new EmailEventExtractionResult(new List<EmailEventCandidate>());
        var validated = extracted.Events.Count > 0
            ? await _calendarValidationAgent.ValidateAsync(extracted, cancellationToken)
            : new CalendarValidationResult(new List<ValidatedCalendarEvent>());

        var suggestions = validated.Events
            .Where(item => item.ShouldAdd)
            .Select(BuildCalendarSuggestion)
            .ToList();

        var localCalendarEvents = new List<CalendarEvent>();
        foreach (var suggestion in suggestions.Where(item => item.StartUtc.HasValue && request.AddEventsToLocalCalendar))
        {
            var localEvent = new CalendarEvent
            {
                Title = suggestion.Title,
                EventDate = suggestion.StartUtc!.Value,
                Description = suggestion.Description
            };
            _dbContext.Events.Add(localEvent);
            suggestion.AddedToLocalCalendar = true;
            localCalendarEvents.Add(localEvent);
        }

        var googleEventsAdded = 0;
        if (request.AddEventsToGoogleCalendar)
        {
            foreach (var suggestion in suggestions.Where(item => item.StartUtc.HasValue))
            {
                var googleEventId = await _gmailMailbox.CreateGoogleCalendarEventAsync(suggestion, cancellationToken);
                suggestion.AddedToGoogleCalendar = !string.IsNullOrWhiteSpace(googleEventId);
                if (suggestion.AddedToGoogleCalendar)
                {
                    googleEventsAdded++;
                }
            }
        }

        var labelsApplied = false;
        if (request.ApplyGmailLabels)
        {
            await _gmailMailbox.ApplyCategoryAsync(message.GmailMessageId, parsedCategory, cancellationToken);
            labelsApplied = true;
        }

        var actionItems = summary.ActionItems ?? new List<string>();
        var embeddingJson = await _memory.CreateEmbeddingJsonAsync(
            $"{message.Subject}\n{summary.DetailedSummary}\n{string.Join("\n", actionItems)}",
            cancellationToken);

        var processedEmail = new ProcessedEmail
        {
            GmailMessageId = message.GmailMessageId,
            GmailThreadId = message.GmailThreadId,
            InternetMessageId = message.InternetMessageId,
            FromAddress = message.FromAddress,
            Subject = message.Subject,
            Snippet = message.Snippet,
            PlainTextBody = message.PlainTextBody,
            Summary = $"{summary.OneLineSummary}\n\n{summary.DetailedSummary}".Trim(),
            ActionItemsJson = JsonSerializer.Serialize(actionItems),
            Category = parsedCategory.ToString(),
            ClassificationReasoning = classification.Reasoning,
            GmailLabelIdsJson = JsonSerializer.Serialize(message.GmailLabelIds),
            EmbeddingJson = embeddingJson,
            ReceivedAtUtc = message.ReceivedAtUtc,
            ProcessedAtUtc = DateTime.UtcNow,
            LabelsApplied = labelsApplied,
            HasCalendarSuggestions = suggestions.Count > 0,
            CalendarSuggestions = suggestions
        };

        await _memory.UpsertProcessedEmailAsync(processedEmail, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EmailProcessingResult(
            message.GmailMessageId,
            message.Subject,
            parsedCategory.ToString(),
            processedEmail.Summary,
            actionItems,
            suggestions.Select(suggestion => new CalendarEventSummary(
                suggestion.Title,
                suggestion.StartUtc,
                suggestion.IsAllDay,
                suggestion.Confidence,
                suggestion.AddedToLocalCalendar,
                suggestion.AddedToGoogleCalendar)).ToList(),
            false,
            localCalendarEvents.Count > 0,
            googleEventsAdded > 0);
    }

    private static EmailCalendarSuggestion BuildCalendarSuggestion(ValidatedCalendarEvent item)
    {
        var startUtc = TryParseDateTime(item.Date, item.StartTime, item.AllDay, out var parsedStart)
            ? parsedStart
            : null;

        var endUtc = TryParseDateTime(item.Date, item.EndTime, item.AllDay, out var parsedEnd)
            ? parsedEnd
            : item.AllDay && parsedStart.HasValue
                ? parsedStart.Value.AddDays(1)
                : parsedStart?.AddHours(1);

        return new EmailCalendarSuggestion
        {
            Title = item.Title,
            Description = item.Description,
            RawDateText = item.Date,
            RawStartTimeText = item.StartTime,
            RawEndTimeText = item.EndTime,
            IsAllDay = item.AllDay,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Confidence = item.Confidence
        };
    }

    private static bool TryParseDateTime(string date, string time, bool allDay, out DateTime? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(date))
        {
            return false;
        }

        if (allDay || string.IsNullOrWhiteSpace(time))
        {
            if (DateTime.TryParse(date, out var dayOnly))
            {
                parsed = DateTime.SpecifyKind(dayOnly.Date, DateTimeKind.Utc);
                return true;
            }

            return false;
        }

        if (DateTime.TryParse($"{date} {time}", out var dateTime))
        {
            parsed = DateTime.SpecifyKind(dateTime.ToUniversalTime(), DateTimeKind.Utc);
            return true;
        }

        return false;
    }
}

public sealed record EmailSyncRequest(
    int MaxResults,
    string? Query,
    bool IncludeSpamTrash,
    bool ApplyGmailLabels,
    bool AddEventsToLocalCalendar,
    bool AddEventsToGoogleCalendar,
    bool ForceReprocess);

public sealed record EmailSyncSummary(
    int RequestedCount,
    int SkippedCount,
    int ProcessedCount,
    int ImportantCount,
    int PromotionsCount,
    int SpamCount,
    int EmailsWithLocalCalendarEvents,
    int EmailsWithGoogleCalendarEvents,
    IReadOnlyList<EmailProcessingResult> Results);

public sealed record EmailProcessingResult(
    string GmailMessageId,
    string Subject,
    string Category,
    string Summary,
    IReadOnlyList<string> ActionItems,
    IReadOnlyList<CalendarEventSummary> SuggestedEvents,
    bool WasSkipped,
    bool EventsAddedToLocalCalendar,
    bool EventsAddedToGoogleCalendar);

public sealed record CalendarEventSummary(
    string Title,
    DateTime? StartUtc,
    bool IsAllDay,
    double Confidence,
    bool AddedToLocalCalendar,
    bool AddedToGoogleCalendar);
