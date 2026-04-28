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
                instructions: @"Your name is Alfred. You are an intelligent assistant for a university student.

TOOLS:
- ReadPdf(filePath): Reads a PDF. Pass just the filename like 'kiethmillssyllabus'. NEVER use MCP tools to read PDFs.
- ReadWord(filePath): Reads a Word doc. Pass just the filename.
- AddToCalendar(title, dateStr, description): Saves an event. Use YYYY-MM-DD format for dateStr.
- GetWeatherAndTime(location): Gets weather and time.
- AskEmailAgent(text): Reads recent emails.

STRICT RULES:
1. To read a PDF or Word file, call ReadPdf or ReadWord with just the filename. Never use MCP filesystem tools for this.
2. After reading a document OR scanning emails, you MUST immediately call AddToCalendar for EACH important date found. Do NOT ask for permission. Do NOT say 'let me add this'. Just call AddToCalendar right away.
3. After adding all events, confirm to the user what was added.
4. Never respond with raw code blocks or JSON. Always respond in plain conversational text.
5. If you find a date in an email or document, your next action MUST be calling AddToCalendar — no exceptions.",
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