using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

public sealed class LocalMailboxService
{
    private readonly LocalMailboxOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public LocalMailboxService(IOptions<LocalMailboxOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public string ResolvedSampleDirectory =>
        Path.IsPathRooted(_options.SampleDirectory)
            ? _options.SampleDirectory
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.SampleDirectory));

    public async Task<IReadOnlyList<InboxEmailMessage>> GetRecentMessagesAsync(
        int maxResults,
        string? query,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ResolvedSampleDirectory);
        return await LocalMailboxReader.LoadAsync(
            ResolvedSampleDirectory,
            maxResults,
            query,
            includeArchived,
            _jsonOptions,
            cancellationToken);
    }

    public IReadOnlyList<string> GetLabelsForCategory(EmailCategory category)
    {
        return EmailCategoryLabels.GetLabels(category);
    }
}

public static class LocalMailboxReader
{
    public static async Task<IReadOnlyList<InboxEmailMessage>> LoadAsync(
        string directoryPath,
        int maxResults,
        string? query,
        bool includeArchived,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<InboxEmailMessage>();
        }

        var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
        var messages = new List<InboxEmailMessage>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = File.OpenRead(file);
            var email = await JsonSerializer.DeserializeAsync<LocalMailboxEmailDocument>(stream, jsonOptions, cancellationToken);
            if (email is null)
            {
                continue;
            }

            if (!includeArchived && email.IsArchived)
            {
                continue;
            }

            var message = email.ToInboxEmailMessage();
            if (!MatchesQuery(message, query))
            {
                continue;
            }

            messages.Add(message);
        }

        return messages
            .OrderByDescending(message => message.ReceivedAtUtc)
            .Take(Math.Clamp(maxResults, 1, 100))
            .ToList();
    }

    public static bool MatchesQuery(InboxEmailMessage message, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var indexedWords = Regex
            .Split(
                string.Join(
                    "\n",
                    message.Subject,
                    message.FromAddress,
                    message.Snippet,
                    message.PlainTextBody,
                    string.Join(" ", message.GmailLabelIds)).ToLowerInvariant(),
                @"[^a-z0-9]+")
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToHashSet(StringComparer.Ordinal);

        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(token => indexedWords.Contains(token.ToLowerInvariant()));
    }
}

public sealed class LocalMailboxEmailDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ThreadId { get; set; } = string.Empty;
    public string InternetMessageId { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string PlainTextBody { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = new();
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }

    public InboxEmailMessage ToInboxEmailMessage()
    {
        return new InboxEmailMessage(
            Id,
            string.IsNullOrWhiteSpace(ThreadId) ? Id : ThreadId,
            InternetMessageId,
            Subject,
            FromAddress,
            Snippet,
            PlainTextBody,
            Labels,
            DateTime.SpecifyKind(ReceivedAtUtc, DateTimeKind.Utc));
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
                Current Labels: {string.Join(", ", GmailLabelIds)}
                Snippet: {Snippet}
                Body:
                {PlainTextBody}
                """;
    }
}
