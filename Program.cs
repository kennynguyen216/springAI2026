using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=NewAgentDB.db"));

builder.Services.AddSingleton<AgentSessionManager>();

builder.Services.AddScoped<IChatClient>(sp =>
{
        var options = new OpenAIClientOptions()
        {
            Endpoint = new Uri(builder.Configuration["Chat:Endpoint"])
        };
    var model = builder.Configuration["Chat:Model"];

    IChatClient openAiClient = new OpenAIClient(new ApiKeyCredential("test123"), options).GetChatClient(model).AsIChatClient();
    return openAiClient;
});

builder.Services.AddScoped<SensitivityClassifier>();

builder.Services.AddAlfredAgent();
builder.Services.AddEmailAgent();
builder.Services.AddCalendarAgent();

var app = builder.Build();

// Static Files Configuration
var frontendPath = Path.Combine(builder.Environment.ContentRootPath, "frontend");
if (Directory.Exists(frontendPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(frontendPath) });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(frontendPath) });
}

// Database Initialization
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // EnsureCreated checks for the models defined in your Database.cs
    dbContext.Database.EnsureCreated();
}

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTheme(ScalarTheme.DeepSpace));

// Main Chat Logic
app.MapPost("/chat", async (ChatRequest request, IServiceProvider sp, AppDbContext db, AgentSessionManager manager, ILogger<Program> logger) =>
{
    try
    {
        string threadId = request.ThreadId ?? Guid.NewGuid().ToString("N");
        // Recall History using models from Database.cs
        var dbMessages = await db.Messages
            .Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        var historyMessages = new List<Microsoft.Extensions.AI.ChatMessage>();
        foreach (var m in dbMessages)
        {
            var role = m.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant;
            historyMessages.Add(new Microsoft.Extensions.AI.ChatMessage(role, m.Content));
        }

        historyMessages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, request.Message));

        var alfredCapabilities = new List<AITool>
        {
            AIFunctionFactory.Create(AlfredAgent.AlfredCapabilities.AskEmailAgent),
            AIFunctionFactory.Create(AlfredAgent.AlfredCapabilities.AskCalendarAgent),
            AIFunctionFactory.Create(AgentTools.GetWeatherAndTime),
            AIFunctionFactory.Create(AgentTools.ReadPdf),
            AIFunctionFactory.Create(AgentTools.ReadWord),
            AIFunctionFactory.Create(AgentTools.ReadRecentEmails),
            AIFunctionFactory.Create(
                (string title, string dateStr, string description, string? time) =>
                    AgentTools.AddToCalendar(title, dateStr, description, db, time),
                "AddToCalendar",
                "Saves a new event to the user's personal calendar database. time is optional — only provide it if a specific time is mentioned."
            ),
        };

        var allTools = alfredCapabilities;

        var runOptions = new ChatClientAgentRunOptions 
        { 
            ChatOptions = new ChatOptions { Tools = allTools, Temperature = 0.0f } 
        };

        var agent = sp.GetRequiredKeyedService<AIAgent>("ChatAgent");

        if (!manager.Sessions.TryGetValue(threadId, out var agentSession))
        {
            agentSession = await agent.CreateSessionAsync();
            manager.Sessions[threadId] = agentSession;
        }

        var result = await ((ChatClientAgent)agent).RunAsync(historyMessages, agentSession, runOptions);
        var responseText = result.Text ?? "No response.";

        // Save history to your SQLite DB
        db.Messages.Add(new ChatMessage { ThreadId = threadId, Role = "user", Content = request.Message, Timestamp = DateTime.UtcNow });
        db.Messages.Add(new ChatMessage { ThreadId = threadId, Role = "assistant", Content = responseText, Timestamp = DateTime.UtcNow });
        await db.SaveChangesAsync();

        return Results.Ok(new ChatResponse(responseText, threadId));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Chat Error");
        return Results.Problem(ex.Message);
    }
});

// Scan Inbox and Auto-Add to Calendar
app.MapPost("/scan-inbox", async (AppDbContext db, IChatClient chatClient, SensitivityClassifier classifier, ILogger<Program> logger) =>
{
    try
    {
        var emailText = await AgentTools.ReadRecentEmails();
        if (emailText.StartsWith("I was able to connect"))
            return Results.Problem("Could not read emails.");

        // Split individual emails and run each through the classifier
        var emails = emailText.Split("---", StringSplitOptions.RemoveEmptyEntries);
        var relevantEmails = new List<string>();
        int blockedCount = 0;
        int irrelevantCount = 0;

        foreach (var email in emails)
        {
            var classification = await classifier.ClassifyAsync(email.Trim());
            if (classification == "relevant")
                relevantEmails.Add(email.Trim());
            else if (classification == "sensitive")
            {
                blockedCount++;
                Console.WriteLine($"[CLASSIFIER] Blocked sensitive email.");
            }
            else
            {
                irrelevantCount++;
                Console.WriteLine($"[CLASSIFIER] Skipped irrelevant email.");
            }
        }

        if (relevantEmails.Count == 0)
        {
            string noRelevantMsg = $"No relevant emails found. ({irrelevantCount} irrelevant, {blockedCount} sensitive blocked.)";
            return Results.Ok(new { added = 0, message = noRelevantMsg });
        }

        var emailTextFiltered = string.Join("\n\n---\n\n", relevantEmails);

        var prompt = $@"You are a date extraction assistant. Read the following emails and extract any important dates or events.
Return ONLY a valid JSON array with no extra text. Each item must have: ""title"", ""date"" (YYYY-MM-DD format), ""time"" (optional, e.g. '7PM'), ""description"".
If there are no important dates, return an empty array: []

Emails:
{emailTextFiltered}";

        var response = await chatClient.GetResponseAsync(prompt);
        var json = response.Text ?? "[]";

        json = json.Trim();
        if (json.Contains("```"))
            json = System.Text.RegularExpressions.Regex.Match(json, @"\[.*\]", System.Text.RegularExpressions.RegexOptions.Singleline).Value;

        var events = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(json);
        if (events == null || events.Count == 0)
            return Results.Ok(new { added = 0, message = "No important dates found in your inbox." });

        int added = 0;
        var addedTitles = new List<string>();

        foreach (var ev in events)
        {
            var title = ev.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var dateStr = ev.TryGetProperty("date", out var d) ? d.GetString() ?? "" : "";
            var time = ev.TryGetProperty("time", out var ti) ? ti.GetString() : null;
            var description = ev.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(title) || !DateTime.TryParse(dateStr, out var parsedDate))
                continue;

            string finalDescription = string.IsNullOrWhiteSpace(time) ? description : $"{time}{(string.IsNullOrWhiteSpace(description) ? "" : " — " + description)}";

            db.Events.Add(new CalendarEvent { Title = title, EventDate = parsedDate, Description = finalDescription });
            addedTitles.Add($"{title} on {parsedDate:MMMM d, yyyy}{(string.IsNullOrWhiteSpace(time) ? "" : " at " + time)}");
            added++;
        }

        if (added > 0) await db.SaveChangesAsync();

        string summary = $" ({irrelevantCount} irrelevant skipped, {blockedCount} sensitive blocked.)";
        return Results.Ok(new { added, message = added == 0 ? $"No valid dates found.{summary}" : $"Added {added} event(s): " + string.Join(", ", addedTitles) + summary });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Scan inbox error");
        return Results.Problem(ex.Message);
    }
});

// Calendar & History Endpoints
app.MapGet("/events", async (AppDbContext db) => await db.Events.OrderBy(e => e.EventDate).ToListAsync());

app.MapPost("/events", async (CalendarEventRequest req, AppDbContext db) => {
    if (!DateTime.TryParse(req.Date, out var parsed)) return Results.BadRequest("Invalid date");
    var ev = new CalendarEvent { Title = req.Title, EventDate = parsed, Description = req.Description ?? "" };
    db.Events.Add(ev);
    await db.SaveChangesAsync();
    return Results.Ok(ev);
});

app.MapPut("/events/{id}", async (int id, CalendarEventRequest req, AppDbContext db) => {
    var ev = await db.Events.FindAsync(id);
    if (ev == null) return Results.NotFound();
    if (!DateTime.TryParse(req.Date, out var parsed)) return Results.BadRequest("Invalid date");
    ev.Title = req.Title;
    ev.EventDate = parsed;
    ev.Description = req.Description ?? "";
    await db.SaveChangesAsync();
    return Results.Ok(ev);
});

app.MapDelete("/events/{id}", async (int id, AppDbContext db) => {
    var ev = await db.Events.FindAsync(id);
    if (ev == null) return Results.NotFound();
    db.Events.Remove(ev);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.Run();

// API Records
public record ChatRequest(string Message, string? ThreadId = null);
public record ChatResponse(string Response, string ThreadId);
public record CalendarEventRequest(string Title, string Date, string? Description = null);