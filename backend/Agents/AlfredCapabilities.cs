using System.ComponentModel;
using Microsoft.Agents.AI;

/// <summary>
/// Provides Alfred-specific tool capability wrappers.
/// </summary>
public static class AlfredCapabilities
{
    /// <summary>
    /// Asks the email specialist agent to parse text.
    /// </summary>
    /// <param name="text">The raw email text.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="threadId">The current thread identifier.</param>
    /// <returns>The email agent result text.</returns>
    [Description("Asks the Email Specialist to parse text.")]
    public static async Task<string> AskEmailAgent(string text, IServiceProvider serviceProvider, string threadId)
    {
        var agent = serviceProvider.GetRequiredKeyedService<AIAgent>("EmailAgent");
        var sessionManager = serviceProvider.GetRequiredService<AgentSessionManager>();
        var session = await sessionManager.GetOrCreateSessionAsync($"{threadId}_email", agent);
        var result = await agent.RunAsync(text, session);

        return result.Text ?? "No details found.";
    }

    /// <summary>
    /// Asks the calendar specialist agent to schedule an event.
    /// </summary>
    /// <param name="details">The event details.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="threadId">The current thread identifier.</param>
    /// <returns>The calendar agent result text.</returns>
    [Description("Asks the Calendar Specialist to schedule an event.")]
    public static async Task<string> AskCalendarAgent(string details, IServiceProvider serviceProvider, string threadId)
    {
        var agent = serviceProvider.GetRequiredKeyedService<AIAgent>("CalendarAgent");
        var sessionManager = serviceProvider.GetRequiredService<AgentSessionManager>();
        var session = await sessionManager.GetOrCreateSessionAsync($"{threadId}_cal", agent);
        var result = await agent.RunAsync(details, session);

        return result.Text ?? "Event scheduled.";
    }
}
