/// <summary>
/// Represents a persisted chat message in a conversation thread.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Gets or sets the database identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the thread identifier that owns this message.
    /// </summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role associated with the message.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the message was stored.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
