using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using GoogleMessage = Google.Apis.Gmail.v1.Data.Message;

public sealed class GmailMailboxService
{
    private static readonly string[] CategoryLabelsToRemove =
    [
        "CATEGORY_PERSONAL",
        "CATEGORY_PROMOTIONS",
        "CATEGORY_SOCIAL",
        "CATEGORY_UPDATES",
        "CATEGORY_FORUMS"
    ];

    private readonly GoogleWorkspaceService _googleWorkspace;
    private readonly GoogleWorkspaceOptions _options;

    public GmailMailboxService(GoogleWorkspaceService googleWorkspace, Microsoft.Extensions.Options.IOptions<GoogleWorkspaceOptions> options)
    {
        _googleWorkspace = googleWorkspace;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<InboxEmailMessage>> GetRecentMessagesAsync(
        int maxResults,
        string? query,
        bool includeSpamTrash,
        CancellationToken cancellationToken = default)
    {
        var gmail = await _googleWorkspace.CreateGmailServiceAsync(cancellationToken);
        var request = gmail.Users.Messages.List("me");
        request.MaxResults = maxResults;
        request.Q = query;
        request.IncludeSpamTrash = includeSpamTrash;

        var page = await request.ExecuteAsync(cancellationToken);
        if (page.Messages is null || page.Messages.Count == 0)
        {
            return Array.Empty<InboxEmailMessage>();
        }

        var messages = new List<InboxEmailMessage>();
        foreach (var messageRef in page.Messages)
        {
            if (string.IsNullOrWhiteSpace(messageRef.Id))
            {
                continue;
            }

            var getRequest = gmail.Users.Messages.Get("me", messageRef.Id);
            getRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
            var fullMessage = await getRequest.ExecuteAsync(cancellationToken);
            messages.Add(MapMessage(fullMessage));
        }

        return messages
            .OrderByDescending(message => message.ReceivedAtUtc)
            .ToList();
    }

    public async Task ApplyCategoryAsync(string gmailMessageId, EmailCategory category, CancellationToken cancellationToken = default)
    {
        var gmail = await _googleWorkspace.CreateGmailServiceAsync(cancellationToken);

        var addLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var removeLabels = new HashSet<string>(CategoryLabelsToRemove, StringComparer.OrdinalIgnoreCase);

        switch (category)
        {
            case EmailCategory.Spam:
                addLabels.Add("SPAM");
                removeLabels.Add("IMPORTANT");
                removeLabels.Add("INBOX");
                break;

            case EmailCategory.Important:
                addLabels.Add("IMPORTANT");
                removeLabels.Add("SPAM");
                break;

            case EmailCategory.Promotions:
                addLabels.Add("CATEGORY_PROMOTIONS");
                removeLabels.Add("SPAM");
                removeLabels.Add("IMPORTANT");
                break;

            default:
                removeLabels.Add("SPAM");
                break;
        }

        var request = new ModifyMessageRequest
        {
            AddLabelIds = addLabels.ToList(),
            RemoveLabelIds = removeLabels.ToList()
        };

        await gmail.Users.Messages.Modify(request, "me", gmailMessageId).ExecuteAsync(cancellationToken);
    }

    public async Task<string?> CreateGoogleCalendarEventAsync(EmailCalendarSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableGoogleCalendarWrite)
        {
            return null;
        }

        var calendar = await _googleWorkspace.CreateCalendarServiceAsync(cancellationToken);
        var calendarEvent = new Event
        {
            Summary = suggestion.Title,
            Description = suggestion.Description
        };

        if (suggestion.IsAllDay && suggestion.StartUtc.HasValue)
        {
            var startDate = suggestion.StartUtc.Value.ToUniversalTime().Date;
            var endDate = suggestion.EndUtc?.ToUniversalTime().Date ?? startDate.AddDays(1);
            calendarEvent.Start = new EventDateTime { Date = startDate.ToString("yyyy-MM-dd") };
            calendarEvent.End = new EventDateTime { Date = endDate.ToString("yyyy-MM-dd") };
        }
        else if (suggestion.StartUtc.HasValue)
        {
            calendarEvent.Start = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(suggestion.StartUtc.Value, TimeSpan.Zero) };
            calendarEvent.End = new EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(suggestion.EndUtc ?? suggestion.StartUtc.Value.AddHours(1), TimeSpan.Zero)
            };
        }
        else
        {
            return null;
        }

        var insert = calendar.Events.Insert(calendarEvent, _options.CalendarId);
        var created = await insert.ExecuteAsync(cancellationToken);
        return created.Id;
    }

    private static InboxEmailMessage MapMessage(GoogleMessage message)
    {
        var payload = message.Payload;
        var subject = GetHeader(payload, "Subject");
        var from = GetHeader(payload, "From");
        var internetMessageId = GetHeader(payload, "Message-Id");
        var plainTextBody = ExtractPlainTextBody(payload);
        var labelIds = message.LabelIds ?? Array.Empty<string>();

        return new InboxEmailMessage(
            message.Id ?? string.Empty,
            message.ThreadId ?? string.Empty,
            internetMessageId,
            subject,
            from,
            message.Snippet ?? string.Empty,
            plainTextBody,
            labelIds.ToList(),
            message.InternalDate.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(message.InternalDate.Value).UtcDateTime
                : DateTime.UtcNow);
    }

    private static string GetHeader(MessagePart? payload, string name)
    {
        return payload?.Headers?
            .FirstOrDefault(header => name.Equals(header.Name, StringComparison.OrdinalIgnoreCase))?
            .Value ?? string.Empty;
    }

    private static string ExtractPlainTextBody(MessagePart? part)
    {
        if (part is null)
        {
            return string.Empty;
        }

        if (string.Equals(part.MimeType, "text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return DecodeBody(part.Body?.Data);
        }

        if (part.Parts is not null)
        {
            foreach (var child in part.Parts)
            {
                var text = ExtractPlainTextBody(child);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            foreach (var child in part.Parts)
            {
                if (string.Equals(child.MimeType, "text/html", StringComparison.OrdinalIgnoreCase))
                {
                    return StripHtml(DecodeBody(child.Body?.Data));
                }
            }
        }

        if (string.Equals(part.MimeType, "text/html", StringComparison.OrdinalIgnoreCase))
        {
            return StripHtml(DecodeBody(part.Body?.Data));
        }

        return DecodeBody(part.Body?.Data);
    }

    private static string DecodeBody(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return string.Empty;
        }

        var normalized = encoded.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2:
                normalized += "==";
                break;
            case 3:
                normalized += "=";
                break;
        }

        try
        {
            var bytes = Convert.FromBase64String(normalized);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutTags = Regex.Replace(html, "<[^>]+>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(withoutTags), @"\s+", " ").Trim();
    }
}

public sealed record InboxEmailMessage(
    string GmailMessageId,
    string GmailThreadId,
    string InternetMessageId,
    string Subject,
    string FromAddress,
    string Snippet,
    string PlainTextBody,
    IReadOnlyList<string> GmailLabelIds,
    DateTime ReceivedAtUtc)
{
    public string ToContextBlock()
    {
        return $"""
                From: {FromAddress}
                Subject: {Subject}
                Received UTC: {ReceivedAtUtc:O}
                Current Gmail Labels: {string.Join(", ", GmailLabelIds)}
                Snippet: {Snippet}
                Body:
                {PlainTextBody}
                """;
    }
}
