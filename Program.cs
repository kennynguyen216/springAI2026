using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using OpenAI;
using System.ClientModel;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => options.AddDefaultPolicy(p => 
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=NewAgentDB.db"));

builder.Services.AddSingleton<AgentSessionManager>();
builder.Services.Configure<ChatModelOptions>(builder.Configuration.GetSection("Chat"));
builder.Services.Configure<EmbeddingModelOptions>(builder.Configuration.GetSection("Embeddings"));
builder.Services.Configure<GoogleWorkspaceOptions>(builder.Configuration.GetSection("Google"));
builder.Services.Configure<EmailProcessingOptions>(builder.Configuration.GetSection("EmailProcessing"));

builder.Services.AddScoped<IChatClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<ChatModelOptions>>().Value;
    var options = new OpenAIClientOptions
    {
        Endpoint = new Uri(settings.Endpoint)
    };

    var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? "test123" : settings.ApiKey;
    var model = settings.Model;

    IChatClient openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options)
        .GetChatClient(model)
        .AsIChatClient();
    return openAiClient;
});

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<EmbeddingModelOptions>>().Value;
    var options = new OpenAIClientOptions
    {
        Endpoint = new Uri(settings.Endpoint)
    };

    var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? "test123" : settings.ApiKey;
    var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
    return client.GetEmbeddingClient(settings.Model).AsIEmbeddingGenerator(settings.Dimensions);
});

builder.Services.AddSingleton<GoogleWorkspaceService>();
builder.Services.AddScoped<GmailMailboxService>();
builder.Services.AddScoped<StructuredAgentRunner>();
builder.Services.AddScoped<EmailClassificationAgent>();
builder.Services.AddScoped<EmailSummaryAgent>();
builder.Services.AddScoped<EmailEventExtractionAgent>();
builder.Services.AddScoped<CalendarValidationAgent>();
builder.Services.AddScoped<EmailMemoryService>();
builder.Services.AddScoped<EmailManagerAgentService>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<EmailMcpTools>();

builder.Services.AddAlfredAgent();
builder.Services.AddEmailAgent();
builder.Services.AddCalendarAgent();

var app = builder.Build();
app.UseCors();

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
    await EnsureEmailTablesAsync(dbContext);
}

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTheme(ScalarTheme.DeepSpace));
app.MapMcp();

// Main Chat Logic
app.MapPost("/chat", async (ChatRequest request, IServiceProvider sp, AppDbContext db, AgentSessionManager manager, ILogger<Program> logger) =>
{
    try
    {
        string threadId = request.ThreadId ?? Guid.NewGuid().ToString("N");
        string userRoot = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-filesystem", userRoot]
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
            AIFunctionFactory.Create(AgentTools.SyncAndOrganizeRecentEmails),
            AIFunctionFactory.Create(AgentTools.SearchOrganizedEmails),
            AIFunctionFactory.Create(AgentTools.GetGmailConnectionStatus),
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

app.MapDelete("/events/{id}", async (int id, AppDbContext db) => {
    var ev = await db.Events.FindAsync(id);
    if (ev == null) return Results.NotFound();
    db.Events.Remove(ev);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapGet("/gmail/status", async (GoogleWorkspaceService googleWorkspace, CancellationToken cancellationToken) =>
    Results.Ok(await googleWorkspace.GetDetailedStatusAsync(cancellationToken)));

app.MapPost("/email/process", async (EmailProcessRequest request, EmailManagerAgentService manager, IOptions<EmailProcessingOptions> options, CancellationToken cancellationToken) =>
{
    var defaults = options.Value;
    var syncRequest = new EmailSyncRequest(
        request.MaxResults ?? defaults.DefaultSyncCount,
        request.Query ?? defaults.DefaultQuery,
        request.IncludeSpamTrash ?? true,
        request.ApplyGmailLabels ?? defaults.ApplyGmailLabelsByDefault,
        request.AddEventsToLocalCalendar ?? defaults.AddEventsToLocalCalendarByDefault,
        request.AddEventsToGoogleCalendar ?? defaults.AddEventsToGoogleCalendarByDefault,
        request.ForceReprocess);

    var summary = await manager.ProcessInboxAsync(syncRequest, cancellationToken);
    return Results.Ok(summary);
});

app.MapGet("/emails", async (AppDbContext db, string? category, int? limit, CancellationToken cancellationToken) =>
{
    var query = db.ProcessedEmails
        .AsNoTracking()
        .Include(email => email.CalendarSuggestions)
        .OrderByDescending(email => email.ReceivedAtUtc)
        .AsQueryable();

    if (!string.IsNullOrWhiteSpace(category))
    {
        query = query.Where(email => email.Category == category);
    }

    var take = Math.Clamp(limit ?? 50, 1, 200);
    var emails = await query
        .Take(take)
        .Select(email => new
        {
            email.Id,
            email.Subject,
            email.FromAddress,
            email.Category,
            email.Summary,
            email.ReceivedAtUtc,
            email.LabelsApplied,
            CalendarSuggestionCount = email.CalendarSuggestions.Count
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(emails);
});

app.MapGet("/emails/{id:int}", async (int id, AppDbContext db, CancellationToken cancellationToken) =>
{
    var email = await db.ProcessedEmails
        .AsNoTracking()
        .Include(item => item.CalendarSuggestions)
        .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    return email is null ? Results.NotFound() : Results.Ok(email);
});

app.MapGet("/emails/search", async (string query, int? top, EmailManagerAgentService manager, CancellationToken cancellationToken) =>
    Results.Ok(await manager.SearchInboxMemoryAsync(query, Math.Clamp(top ?? 5, 1, 10), cancellationToken)));

app.MapPost("/emails/{id:int}/events", async (int id, bool googleCalendar, EmailManagerAgentService manager, CancellationToken cancellationToken) =>
    Results.Ok(await manager.AddEventsForEmailAsync(id, googleCalendar, cancellationToken)));

static async Task EnsureEmailTablesAsync(AppDbContext dbContext)
{
    await dbContext.Database.ExecuteSqlRawAsync(
        """
        CREATE TABLE IF NOT EXISTS ProcessedEmails (
            Id INTEGER NOT NULL CONSTRAINT PK_ProcessedEmails PRIMARY KEY AUTOINCREMENT,
            GmailMessageId TEXT NOT NULL,
            GmailThreadId TEXT NOT NULL,
            InternetMessageId TEXT NOT NULL,
            FromAddress TEXT NOT NULL,
            Subject TEXT NOT NULL,
            Snippet TEXT NOT NULL,
            PlainTextBody TEXT NOT NULL,
            Summary TEXT NOT NULL,
            ActionItemsJson TEXT NOT NULL,
            Category TEXT NOT NULL,
            ClassificationReasoning TEXT NOT NULL,
            GmailLabelIdsJson TEXT NOT NULL,
            EmbeddingJson TEXT NOT NULL,
            ReceivedAtUtc TEXT NOT NULL,
            ProcessedAtUtc TEXT NOT NULL,
            LabelsApplied INTEGER NOT NULL,
            HasCalendarSuggestions INTEGER NOT NULL
        );
        """);

    await dbContext.Database.ExecuteSqlRawAsync(
        """
        CREATE UNIQUE INDEX IF NOT EXISTS IX_ProcessedEmails_GmailMessageId
        ON ProcessedEmails (GmailMessageId);
        """);

    await dbContext.Database.ExecuteSqlRawAsync(
        """
        CREATE TABLE IF NOT EXISTS EmailCalendarSuggestions (
            Id INTEGER NOT NULL CONSTRAINT PK_EmailCalendarSuggestions PRIMARY KEY AUTOINCREMENT,
            ProcessedEmailId INTEGER NOT NULL,
            Title TEXT NOT NULL,
            Description TEXT NOT NULL,
            RawDateText TEXT NOT NULL,
            RawStartTimeText TEXT NOT NULL,
            RawEndTimeText TEXT NOT NULL,
            IsAllDay INTEGER NOT NULL,
            StartUtc TEXT NULL,
            EndUtc TEXT NULL,
            Confidence REAL NOT NULL,
            AddedToLocalCalendar INTEGER NOT NULL,
            AddedToGoogleCalendar INTEGER NOT NULL,
            FOREIGN KEY (ProcessedEmailId) REFERENCES ProcessedEmails (Id) ON DELETE CASCADE
        );
        """);
}

app.Run();

// API Records
public record ChatRequest(string Message, string? ThreadId = null);
public record ChatResponse(string Response, string ThreadId);
public record EmailProcessRequest(
    int? MaxResults = null,
    string? Query = null,
    bool? IncludeSpamTrash = null,
    bool? ApplyGmailLabels = null,
    bool? AddEventsToLocalCalendar = null,
    bool? AddEventsToGoogleCalendar = null,
    bool ForceReprocess = false);
