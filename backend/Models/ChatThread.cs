/// <summary>
/// Represents a persisted conversation thread for Alfred.
/// </summary>
public class ChatThread
{
    /// <summary>
    /// Gets or sets the unique thread identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the UTC timestamp when the thread was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = new();
}
