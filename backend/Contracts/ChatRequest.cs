/// <summary>
/// Represents a chat request sent by the frontend.
/// </summary>
/// <param name="Message">The user's input message.</param>
/// <param name="ThreadId">The optional conversation thread identifier.</param>
public record ChatRequest(string Message, string? ThreadId = null);
