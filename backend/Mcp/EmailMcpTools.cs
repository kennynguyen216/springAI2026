using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public sealed class EmailMcpTools
{
    [McpServerTool, Description("Syncs recent Gmail messages, classifies them, summarizes them, and adds valid dates to the calendar.")]
    public static Task<EmailSyncSummary> SyncInbox(
        [Description("How many recent Gmail messages to process.")] int maxResults,
        [Description("Optional Gmail search query, for example in:inbox newer_than:7d.")] string? query,
        [Description("Whether to apply Gmail labels like IMPORTANT, SPAM, or CATEGORY_PROMOTIONS.")] bool applyLabels,
        [Description("Whether to add validated events into the local calendar database.")] bool addToLocalCalendar,
        [Description("Whether to add validated events into Google Calendar.")] bool addToGoogleCalendar,
        EmailManagerAgentService manager,
        CancellationToken cancellationToken)
    {
        return manager.ProcessInboxAsync(
            new EmailSyncRequest(
                maxResults,
                query,
                true,
                applyLabels,
                addToLocalCalendar,
                addToGoogleCalendar,
                false),
            cancellationToken);
    }

    [McpServerTool(ReadOnly = true), Description("Searches processed email memory using the project's RAG layer.")]
    public static Task<List<ProcessedEmailSearchHit>> SearchEmails(
        [Description("Natural-language query to search processed email summaries and embeddings.")] string query,
        [Description("Maximum number of results to return.")] int top,
        EmailManagerAgentService manager,
        CancellationToken cancellationToken)
    {
        return manager.SearchInboxMemoryAsync(query, top, cancellationToken);
    }

    [McpServerTool(ReadOnly = true), Description("Reports whether Google OAuth credentials are configured and which account is currently authenticated.")]
    public static Task<GoogleWorkspaceStatus> GmailStatus(
        GoogleWorkspaceService googleWorkspace,
        CancellationToken cancellationToken)
    {
        return googleWorkspace.GetDetailedStatusAsync(cancellationToken);
    }
}
