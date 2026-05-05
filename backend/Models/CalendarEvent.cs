/// <summary>
/// Represents a calendar event stored for the user.
/// </summary>
public class CalendarEvent
{
    /// <summary>
    /// Gets or sets the database identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the event title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the calendar date for the event.
    /// </summary>
    public DateTime EventDate { get; set; }

    /// <summary>
    /// Gets or sets the optional description shown with the event.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
