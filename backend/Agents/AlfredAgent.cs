using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static class AlfredAgent
{
    public static void AddAlfredAgent(this IServiceCollection services)
    {
        services.AddKeyedScoped<AIAgent>("ChatAgent", (sp, key) =>
        {
            var chatClient = sp.GetRequiredService<IChatClient>();
            return new ChatClientAgent(
                chatClient,
                instructions: @"Your name is Alfred. You are the manager agent in a multi-agent productivity system.
                                1. Delegate email understanding work to the specialized agents and tools when the user asks about inbox organization, summaries, or deadlines.
                                2. Use the inbox manager tools before answering questions about local mailbox state whenever fresh email data would help.
                                3. When you reference organized email results, mention the category, summary, and any extracted action items or calendar dates.
                                4. You have access to local document and project tools, so use them when the user asks for recent files, syllabi, notes, or documents.",
                name: "Alfred");
        });
    }

    public static class AlfredCapabilities
    {
        [Description("Asks the Email Specialist to parse text.")]
        public static async Task<string> AskEmailAgent(string text, IServiceProvider sp, string threadId)
        {
            var agent = sp.GetRequiredKeyedService<AIAgent>("EmailAgent");
            var manager = sp.GetRequiredService<AgentSessionManager>();
            var session = await GetSession(threadId, "email", agent, manager);
            var result = await agent.RunAsync(text, session);
            return result.Text ?? "No details found.";
        }

        [Description("Asks the Calendar Specialist to schedule an event.")]
        public static async Task<string> AskCalendarAgent(string details, IServiceProvider sp, string threadId)
        {
            var agent = sp.GetRequiredKeyedService<AIAgent>("CalendarAgent");
            var manager = sp.GetRequiredService<AgentSessionManager>();
            var session = await GetSession(threadId, "cal", agent, manager);
            var result = await agent.RunAsync(details, session);
            return result.Text ?? "Event scheduled.";
        }

        private static async Task<Microsoft.Agents.AI.AgentSession> GetSession(string tId, string suffix, AIAgent agent, AgentSessionManager mgr)
        {
            string key = $"{tId}_{suffix}";
            if (!mgr.Sessions.TryGetValue(key, out var s))
            {
                s = await agent.CreateSessionAsync();
                mgr.Sessions[key] = s;
            }
            return s;
        }
    }
}
