using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

/// <summary>
/// Registers the email specialist agent.
/// </summary>
public static class EmailAgent
{
    /// <summary>
    /// Adds the email specialist agent registration to the service collection.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    public static void AddEmailAgent(this IServiceCollection services)
    {
        services.AddKeyedScoped<AIAgent>("EmailAgent", (sp, key) =>
        {
            var chatClient = sp.GetRequiredService<IChatClient>();
            return new ChatClientAgent(
                chatClient,
                instructions: @"You are the Email Specialist Agent. Your sole purpose is to receive raw email text and extract actionable event information.
                                1. Identify the 'Event Title', 'Date', 'Start Time', and 'End Time'.
                                2. If information is missing, note that it is missing.
                                3. Do not engage in small talk. Output only extracted details.
                                4. If no events are found, state: 'No event details detected.'",
                name: "EmailAgent");
        });
    }
}
