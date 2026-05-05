/// <summary>
/// Represents a request to create or update a calendar event.
/// </summary>
/// <param name="Title">The event title.</param>
/// <param name="Date">The event date supplied by the client.</param>
/// <param name="Description">The optional event description.</param>
public record CalendarEventRequest(string Title, string Date, string? Description = null);
