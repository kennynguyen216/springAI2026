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
                                1. Verify that an extracted event is clear enough to add to a calendar.
                                2. Approve all-day events when the date is clear but a time is not.
                                3. Reject vague event candidates and explain what is missing.
                                4. Once the event is validated, confirm it clearly to Alfred.",
                name: "CalendarAgent");
        });
    }
}
