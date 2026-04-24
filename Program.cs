using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Client;
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
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string downloadsPath = Path.Combine(userProfile, "Downloads");

        
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-filesystem", desktopPath, downloadsPath]
        });

        await using var mcpClient = await McpClient.CreateAsync(transport);
        var mcpTools = await mcpClient.ListToolsAsync();

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
            AIFunctionFactory.Create(AgentTools.AddToCalendar),
        };

        var allTools = mcpTools.Cast<AITool>().Concat(alfredCapabilities).ToList();

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