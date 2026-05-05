using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using System.Text;

/// <summary>
/// Coordinates the end-to-end Alfred chat flow.
/// </summary>
public class AlfredChatService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppDbContext _dbContext;
    private readonly AgentSessionManager _sessionManager;
    private readonly AlfredInputGuardrail _inputGuardrail;
    private readonly AlfredOutputGuardrail _outputGuardrail;
    private readonly AlfredInteractionLogger _interactionLogger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlfredChatService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The root service provider.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="sessionManager">The in-memory session manager.</param>
    /// <param name="inputGuardrail">The input guardrail classifier.</param>
    /// <param name="outputGuardrail">The output guardrail validator.</param>
    /// <param name="interactionLogger">The interaction logger.</param>
    public AlfredChatService(
        IServiceProvider serviceProvider,
        AppDbContext dbContext,
        AgentSessionManager sessionManager,
        AlfredInputGuardrail inputGuardrail,
        AlfredOutputGuardrail outputGuardrail,
        AlfredInteractionLogger interactionLogger)
    {
        _serviceProvider = serviceProvider;
        _dbContext = dbContext;
        _sessionManager = sessionManager;
        _inputGuardrail = inputGuardrail;
        _outputGuardrail = outputGuardrail;
        _interactionLogger = interactionLogger;
    }

    /// <summary>
    /// Processes a chat request through guardrails, Alfred, and persistence.
    /// </summary>
    /// <param name="request">The incoming chat request.</param>
    /// <param name="cancellationToken">The cancellation token for the request.</param>
    /// <returns>The chat response for the frontend.</returns>
    public async Task<ChatResponse> HandleAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var threadId = request.ThreadId ?? Guid.NewGuid().ToString("N");

        if (!await _inputGuardrail.IsAllowedAsync(request.Message, cancellationToken))
        {
            await _interactionLogger.LogAsync(request.Message, "BLOCKED", null, cancellationToken);
            return new ChatResponse(AlfredGuardrailPolicy.RefusalMessage, threadId);
        }

        var historyMessages = await BuildHistoryAsync(threadId, request.Message, cancellationToken);
        var agent = _serviceProvider.GetRequiredKeyedService<AIAgent>("ChatAgent");
        var session = await _sessionManager.GetOrCreateSessionAsync(threadId, agent);
        var runOptions = CreateRunOptions();

        var result = await ((ChatClientAgent)agent).RunAsync(historyMessages, session, runOptions);
        var responseText = await _outputGuardrail.ValidateAsync(
            request.Message,
            result.Text ?? "No response.",
            cancellationToken);

        await SaveConversationAsync(threadId, request.Message, responseText, cancellationToken);
        await _interactionLogger.LogAsync(request.Message, "ALLOWED", responseText, cancellationToken);

        return new ChatResponse(responseText, threadId);
    }

    /// <summary>
    /// Streams Alfred's response updates for the given request.
    /// </summary>
    /// <param name="request">The incoming chat request.</param>
    /// <param name="cancellationToken">The cancellation token for the request.</param>
    /// <returns>An async stream of chat events for the client.</returns>
    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var threadId = request.ThreadId ?? Guid.NewGuid().ToString("N");
        yield return new ChatStreamEvent("meta", threadId);

        if (!await _inputGuardrail.IsAllowedAsync(request.Message, cancellationToken))
        {
            await _interactionLogger.LogAsync(request.Message, "BLOCKED", null, cancellationToken);
            yield return new ChatStreamEvent("replace", threadId, AlfredGuardrailPolicy.RefusalMessage);
            yield return new ChatStreamEvent("done", threadId);
            yield break;
        }

        var historyMessages = await BuildHistoryAsync(threadId, request.Message, cancellationToken);
        var agent = _serviceProvider.GetRequiredKeyedService<AIAgent>("ChatAgent");
        var session = await _sessionManager.GetOrCreateSessionAsync(threadId, agent);
        var runOptions = CreateRunOptions();
        var responseBuilder = new StringBuilder();

        await foreach (var update in ((ChatClientAgent)agent).RunStreamingAsync(
                           historyMessages,
                           session,
                           runOptions,
                           cancellationToken))
        {
            if (string.IsNullOrEmpty(update.Text))
            {
                continue;
            }

            responseBuilder.Append(update.Text);
            yield return new ChatStreamEvent("chunk", threadId, update.Text);
        }

        var rawResponseText = responseBuilder.Length == 0
            ? "No response."
            : responseBuilder.ToString();

        var validatedResponse = await _outputGuardrail.ValidateAsync(
            request.Message,
            rawResponseText,
            cancellationToken);

        if (!string.Equals(validatedResponse, rawResponseText, StringComparison.Ordinal))
        {
            // Note: this preserves the validator without altering the streaming pipeline shape.
            yield return new ChatStreamEvent("replace", threadId, validatedResponse);
        }

        await SaveConversationAsync(threadId, request.Message, validatedResponse, cancellationToken);
        await _interactionLogger.LogAsync(request.Message, "ALLOWED", validatedResponse, cancellationToken);

        yield return new ChatStreamEvent("done", threadId);
    }

    private async Task<List<Microsoft.Extensions.AI.ChatMessage>> BuildHistoryAsync(
        string threadId,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var dbMessages = await _dbContext.Messages
            .Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync(cancellationToken);

        var historyMessages = dbMessages
            .Select(MapMessage)
            .ToList();

        historyMessages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, userMessage));
        return historyMessages;
    }

    private ChatClientAgentRunOptions CreateRunOptions()
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(AlfredCapabilities.AskEmailAgent),
            AIFunctionFactory.Create(AlfredCapabilities.AskCalendarAgent),
            AIFunctionFactory.Create(WeatherTool.GetWeatherAndTime),
            AIFunctionFactory.Create(DocumentTool.ReadPdf),
            AIFunctionFactory.Create(DocumentTool.ReadWord),
            AIFunctionFactory.Create(EmailTool.ReadRecentEmails),
            AIFunctionFactory.Create(
                (string title, string dateStr, string description, string? time) =>
                    CalendarTool.AddToCalendar(title, dateStr, description, _dbContext, time),
                "AddToCalendar",
                "Saves a new event to the user's personal calendar database. time is optional — only provide it if a specific time is mentioned.")
        };

        return new ChatClientAgentRunOptions
        {
            ChatOptions = new ChatOptions
            {
                Tools = tools,
                Temperature = 0.0f
            }
        };
    }

    private async Task SaveConversationAsync(
        string threadId,
        string userMessage,
        string responseText,
        CancellationToken cancellationToken)
    {
        _dbContext.Messages.Add(new global::ChatMessage
        {
            ThreadId = threadId,
            Role = "user",
            Content = userMessage,
            Timestamp = DateTime.UtcNow
        });

        _dbContext.Messages.Add(new global::ChatMessage
        {
            ThreadId = threadId,
            Role = "assistant",
            Content = responseText,
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Microsoft.Extensions.AI.ChatMessage MapMessage(global::ChatMessage message)
    {
        var role = message.Role.Equals("user", StringComparison.OrdinalIgnoreCase)
            ? ChatRole.User
            : ChatRole.Assistant;

        return new Microsoft.Extensions.AI.ChatMessage(role, message.Content);
    }
}
