/// <summary>
/// Represents a chat response returned to the frontend.
/// </summary>
/// <param name="Response">The assistant response text.</param>
/// <param name="ThreadId">The conversation thread identifier.</param>
public record ChatResponse(string Response, string ThreadId);
