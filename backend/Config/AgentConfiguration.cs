using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static class AgentConfiguration
{
    public static IServiceCollection AddProjectAgents(this IServiceCollection services, IConfiguration config)
    {
        services.AddKeyedScoped<AIAgent>("EmailAgent", (sp, key) =>
        {
            return new ChatClientAgent(
                chatClient: sp.GetRequiredService<IChatClient>(),
                name: "EmailAgent",
                instructions: "Extract event titles, dates, and times from email text."
            );
        });

        services.AddKeyedScoped<AIAgent>("CalendarAgent", (sp, key) =>
        {
            return new ChatClientAgent(
                chatClient: sp.GetRequiredService<IChatClient>(),
                name: "CalendarAgent",
                instructions: "Format and confirm calendar entries for provided events."
            );
        });

        services.AddKeyedScoped<AIAgent>("ChatAgent", (sp, key) =>
        {
            var chatClient = sp.GetRequiredService<IChatClient>();
            
            var tools = new List<AITool>
            {
                AIFunctionFactory.Create(AgentTools.GetWeatherAndTime),
                AIFunctionFactory.Create((string body) => AgentTools.AskEmailAgent(body, sp, "default")),
                AIFunctionFactory.Create((string details) => AgentTools.AskCalendarAgent(details, sp, "default"))
            };

            return new ChatClientAgent(
                chatClient: chatClient,
                name: "Alfred",
                instructions: @"You are Alfred. You are an intelligent assistant with a variety of other agents that are waiting to assist you in the tasks for the user.
                
                SEARCH RULES:
                1. Always use wildcards for file patterns to avoid being too literal. 
                   Example: If the user says 'declaration', search for '*declaration*'.
                2. If the user says 'CSC 4444', search for '*csc4444*'.
                3. Root Path: 'C:\Users\jhong14\OneDrive - Louisiana State University\Desktop'
                4. Never leave the 'path' empty.",
                tools: tools
            );
        });

        return services;
    }
}