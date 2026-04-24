using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
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
builder.Services.Configure<LocalMailboxOptions>(builder.Configuration.GetSection("LocalMailbox"));
builder.Services.Configure<EmailProcessingOptions>(builder.Configuration.GetSection("EmailProcessing"));

builder.Services.AddScoped<IChatClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<ChatModelOptions>>().Value;
    var options = new OpenAIClientOptions
    {
        Endpoint = new Uri(settings.Endpoint)
    };
    var model = settings.Model;
    var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? "test123" : settings.ApiKey;

    IChatClient openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(model).AsIChatClient();
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

    return new OpenAIClient(new ApiKeyCredential(apiKey), options)
        .GetEmbeddingClient(settings.Model)
        .AsIEmbeddingGenerator(settings.Dimensions);
});

builder.Services.AddScoped<LocalMailboxService>();
builder.Services.AddSingleton<LocalDocumentService>();
builder.Services.AddScoped<StructuredAgentRunner>();
builder.Services.AddScoped<EmailClassificationAgent>();
builder.Services.AddScoped<EmailSummaryAgent>();
builder.Services.AddScoped<EmailEventExtractionAgent>();
builder.Services.AddScoped<CalendarValidationAgent>();
builder.Services.AddScoped<EmailMemoryService>();
builder.Services.AddScoped<EmailManagerAgentService>();

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
}

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTheme(ScalarTheme.DeepSpace));

// Main Chat Logic
app.MapPost("/chat", async (ChatRequest request, IServiceProvider sp, AppDbContext db, AgentSessionManager manager, ILogger<Program> logger) =>
{
    try
    {
        string threadId = request.ThreadId ?? Guid.NewGuid().ToString("N");
        if (DocumentQueryRouter.TryGetMostRecentKeyword(request.Message, out var keyword))
        {
            var documents = sp.GetRequiredService<LocalDocumentService>();
            var routedResponse = documents.DescribeMostRecentDocument(keyword);

            db.Messages.Add(new ChatMessage { ThreadId = threadId, Role = "user", Content = request.Message, Timestamp = DateTime.UtcNow });
            db.Messages.Add(new ChatMessage { ThreadId = threadId, Role = "assistant", Content = routedResponse, Timestamp = DateTime.UtcNow });
            await db.SaveChangesAsync();

            return Results.Ok(new ChatResponse(routedResponse, threadId));
        }

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
            AIFunctionFactory.Create(AgentTools.FindRecentDocuments),
            AIFunctionFactory.Create(AgentTools.GetMostRecentDocument),
            AIFunctionFactory.Create(AgentTools.ReadRecentEmails),
            AIFunctionFactory.Create(AgentTools.SyncAndOrganizeRecentEmails),
            AIFunctionFactory.Create(AgentTools.SearchOrganizedEmails),
            AIFunctionFactory.Create(AgentTools.GetLocalMailboxStatus),
            AIFunctionFactory.Create(AgentTools.AddToCalendar),
        };

        var runOptions = new ChatClientAgentRunOptions 
        { 
            ChatOptions = new ChatOptions { Tools = alfredCapabilities, Temperature = 0.0f } 
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

app.MapGet("/mailbox/status", (LocalMailboxService mailbox) =>
    Results.Ok(new
    {
        sampleDirectory = mailbox.ResolvedSampleDirectory,
        exists = Directory.Exists(mailbox.ResolvedSampleDirectory)
    }));

app.MapPost("/email/process", async (EmailProcessRequest request, EmailManagerAgentService manager, IOptions<EmailProcessingOptions> options, CancellationToken cancellationToken) =>
{
    var defaults = options.Value;
    var syncRequest = new EmailSyncRequest(
        request.MaxResults ?? defaults.DefaultSyncCount,
        request.Query ?? defaults.DefaultQuery,
        request.IncludeSpamTrash ?? true,
        request.ApplyLocalLabels ?? defaults.ApplyLocalLabelsByDefault,
        request.AddEventsToLocalCalendar ?? defaults.AddEventsToLocalCalendarByDefault,
        request.ForceReprocess);

    return Results.Ok(await manager.ProcessInboxAsync(syncRequest, cancellationToken));
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

    return Results.Ok(await query.Take(Math.Clamp(limit ?? 50, 1, 200)).ToListAsync(cancellationToken));
});

app.MapGet("/emails/search", async (string query, int? top, EmailManagerAgentService manager, CancellationToken cancellationToken) =>
    Results.Ok(await manager.SearchInboxMemoryAsync(query, Math.Clamp(top ?? 5, 1, 10), cancellationToken)));

app.MapPost("/emails/{id:int}/events", async (int id, EmailManagerAgentService manager, CancellationToken cancellationToken) =>
    Results.Ok(await manager.AddEventsForEmailAsync(id, cancellationToken)));

app.Run();

// API Records
public record ChatRequest(string Message, string? ThreadId = null);
public record ChatResponse(string Response, string ThreadId);
public record EmailProcessRequest(
    int? MaxResults = null,
    string? Query = null,
    bool? IncludeSpamTrash = null,
    bool? ApplyLocalLabels = null,
    bool? AddEventsToLocalCalendar = null,
    bool ForceReprocess = false);
