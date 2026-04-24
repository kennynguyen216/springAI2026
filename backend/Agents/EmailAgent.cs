using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
public static class EmailAgent
{
    public static void AddEmailAgent(this IServiceCollection services)
    {
        services.AddKeyedScoped<AIAgent>("EmailAgent", (sp, key) =>
        {
            var chatClient = sp.GetRequiredService<IChatClient>();
            return new ChatClientAgent(
                chatClient,
                instructions: @"You are the Email Specialist Agent.
                                1. Read raw email content and focus on dates, deadlines, action items, and schedule details.
                                2. Extract only what is explicitly supported by the message.
                                3. If no events are present, say: 'No event details detected.'
                                4. Do not engage in small talk.",
                name: "EmailAgent");
        });
    }
}
