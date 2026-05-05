using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

/// <summary>
/// Registers Alfred's primary agent definition.
/// </summary>
public static class AlfredAgent
{
    /// <summary>
    /// Adds Alfred's chat agent registration to the service collection.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    public static void AddAlfredAgent(this IServiceCollection services)
    {
        services.AddKeyedScoped<AIAgent>("ChatAgent", (sp, key) =>
        {
            var chatClient = sp.GetRequiredService<IChatClient>();
            return new ChatClientAgent(
                chatClient,
                instructions: $"""
{AlfredGuardrailPolicy.SystemPrompt}

TOOLS:
- ReadPdf(filePath): Reads a PDF. Pass just the filename like 'kiethmillssyllabus'. Never use MCP tools to read PDFs.
- ReadWord(filePath): Reads a Word doc. Pass just the filename.
- AddToCalendar(title, dateStr, description): Saves an event. Use YYYY-MM-DD format for dateStr.
- GetWeatherAndTime(location): Gets weather and time.
- AskEmailAgent(text): Reads recent emails.

TOOL RULES:
1. To read a PDF or Word file, call ReadPdf or ReadWord with just the filename.
2. When a school/work document or email contains important dates, add those dates to the calendar and then confirm what was added.
3. Never respond with raw code blocks or JSON. Always respond in plain conversational text.

PRIVACY RULES:
- Never repeat, display, or store Social Security Numbers, government IDs, or tax information.
- Never repeat, display, or store passwords, PINs, API keys, or any credentials.
- Never repeat, display, or store bank account numbers, credit card numbers, or financial account details.
- Never repeat, display, or store medical records, diagnoses, prescriptions, or health information.
- Never repeat, display, or store private personal identifiers such as passport numbers or driver's license numbers.
- If you encounter this information in a document or email, respond with: 'I found sensitive information in this content and cannot display it for privacy reasons.'
""",
                name: "Alfred");
        });
    }
}
