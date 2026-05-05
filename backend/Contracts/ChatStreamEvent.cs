/// <summary>
/// Represents one streaming chat event emitted by the backend.
/// </summary>
/// <param name="Type">The event type, such as meta, chunk, replace, done, or error.</param>
/// <param name="ThreadId">The conversation thread identifier.</param>
/// <param name="Text">The optional text payload for the event.</param>
public record ChatStreamEvent(string Type, string ThreadId, string? Text = null);
