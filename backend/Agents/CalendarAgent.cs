using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static class CalendarAgent
{
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