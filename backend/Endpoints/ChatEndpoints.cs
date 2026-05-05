using System.Text.Json;

/// <summary>
/// Maps chat-related API endpoints.
/// </summary>
public static class ChatEndpoints
{
    /// <summary>
    /// Maps the chat endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/chat", HandleChatAsync);
        endpoints.MapPost("/chat/stream", HandleChatStreamAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleChatAsync(
        ChatRequest request,
        AlfredChatService chatService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(ChatEndpoints));

        try
        {
            var response = await chatService.HandleAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat Error");
            return Results.Problem(ex.Message);
        }
    }

    private static async Task HandleChatStreamAsync(
        ChatRequest request,
        AlfredChatService chatService,
        HttpResponse response,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(ChatEndpoints));

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "application/x-ndjson";
        response.Headers.CacheControl = "no-cache";

        try
        {
            await foreach (var chatEvent in chatService.StreamAsync(request, cancellationToken))
            {
                await WriteStreamEventAsync(response, chatEvent, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat Stream Error");
            await WriteStreamEventAsync(
                response,
                new ChatStreamEvent("error", request.ThreadId ?? string.Empty, ex.Message),
                cancellationToken);
        }
    }

    private static async Task WriteStreamEventAsync(
        HttpResponse response,
        ChatStreamEvent chatEvent,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = chatEvent.Type,
            threadId = chatEvent.ThreadId,
            text = chatEvent.Text
        });

        await response.WriteAsync(payload + "\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
