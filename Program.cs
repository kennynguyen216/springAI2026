using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Client;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

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

// Register Separated Agents
builder.Services.AddAlfredAgent();
builder.Services.AddEmailAgent();
builder.Services.AddCalendarAgent();

var app = builder.Build();

// Static Files & Frontend Configuration
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
    dbContext.Database.EnsureDeleted();
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
        
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-filesystem", @"C:\Users\jhong14\OneDrive - Louisiana State University\Desktop", @"C:\Users\jhong14\Downloads"]
        });

        await using var mcpClient = await McpClient.CreateAsync(transport);
        var mcpTools = await mcpClient.ListToolsAsync();

        // Recall History from DB
        var dbMessages = await db.Messages
            .Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        // Map messages to the correct immutable ChatMessage type
        var historyMessages = new List<Microsoft.Extensions.AI.ChatMessage>();
        foreach (var m in dbMessages)
        {
            var role = m.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant;
            historyMessages.Add(new Microsoft.Extensions.AI.ChatMessage(role, m.Content));
        }

        // Add current user message to the context list
        historyMessages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, request.Message));

        var alfredCapabilities = new List<AITool>
        {
            AIFunctionFactory.Create(AlfredAgent.AlfredCapabilities.AskEmailAgent),
            AIFunctionFactory.Create(AlfredAgent.AlfredCapabilities.AskCalendarAgent),
            AIFunctionFactory.Create(AgentTools.GetWeatherAndTime),
            AIFunctionFactory.Create(AgentTools.ReadPdf),
            AIFunctionFactory.Create(AgentTools.ReadWord),
            AIFunctionFactory.Create(AgentTools.ReadRecentEmails)
        };

        var allTools = mcpTools.Cast<AITool>().Concat(alfredCapabilities).ToList();

        var runOptions = new ChatClientAgentRunOptions 
        { 
            ChatOptions = new ChatOptions 
            { 
                Tools = allTools,
                //StopSequences = ["}}"],
                Temperature = 0.0f
            } 
        };

        var agent = sp.GetRequiredKeyedService<AIAgent>("ChatAgent");

        if (!manager.Sessions.TryGetValue(threadId, out var agentSession))
        {
            agentSession = await agent.CreateSessionAsync();
            manager.Sessions[threadId] = agentSession;
        }

        var result = await ((ChatClientAgent)agent).RunAsync(historyMessages, agentSession, runOptions);
        var responseText = result.Text ?? "No response.";

        return Results.Ok(new ChatResponse(responseText, threadId));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Chat Error");
        return Results.Problem(ex.Message);
    }
});

// Thread History Endpoints ---
app.MapGet("/threads", async (AppDbContext db) => 
    await db.Threads.Select(t => t.Id).ToListAsync());

app.MapGet("/threads/{id}", async (string id, AppDbContext db) =>
{
    var messages = await db.Messages
        .Where(m => m.ThreadId == id)
        .OrderBy(m => m.Timestamp)
        .Select(m => new { m.Role, m.Content })
        .ToListAsync();
    return Results.Ok(messages);
});

app.Run();

// Data Models ---
public record ChatRequest(string Message, string? ThreadId = null);
public record ChatResponse(string Response, string ThreadId);