using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

/// <summary>
/// Registers the calendar specialist agent.
/// </summary>
public static class CalendarAgent
{
    /// <summary>
    /// Adds the calendar specialist agent registration to the service collection.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    public static void AddCalendarAgent(this IServiceCollection services)
    {
        services.AddKeyedScoped<AIAgent>("CalendarAgent", (sp, key) =>
        {
            var chatClient = sp.GetRequiredService<IChatClient>();
            return new ChatClientAgent(
                chatClient,
                instructions: @"You are the Calendar Specialist Agent.
                                1. Verify an event has a Title, Date, and Time.
                                2. If details are missing, ask Alfred to request them.
                                3. Once all details are present, confirm the summary to Alfred.",
                name: "CalendarAgent");
        });
    }
}
