using System.ComponentModel;

/// <summary>
/// Provides calendar-related tool functions exposed to agents.
/// </summary>
public static class CalendarTool
{
    /// <summary>
    /// Saves a new event to the user's personal calendar database.
    /// </summary>
    /// <param name="title">The event title.</param>
    /// <param name="dateStr">The event date string.</param>
    /// <param name="description">The event description.</param>
    /// <param name="db">The database context.</param>
    /// <param name="time">The optional event time.</param>
    /// <returns>A user-facing status message.</returns>
    [Description("Saves a new event to the user's personal calendar database. time is optional — only provide it if a specific time is mentioned.")]
    public static async Task<string> AddToCalendar(
        string title,
        string dateStr,
        string description,
        AppDbContext db,
        string? time = null)
    {
        try
        {
            var cleanedDate = System.Text.RegularExpressions.Regex.Replace(dateStr, @"(\d+)(st|nd|rd|th)", "$1");
            cleanedDate = cleanedDate.Replace(" at ", " ", StringComparison.Ordinal).Trim();

            Console.WriteLine(
                $"[AddToCalendar] title={title} | dateStr={dateStr} | cleanedDate={cleanedDate} | time={time}");

            if (DateTime.TryParse(cleanedDate, out var parsedDate))
            {
                var finalDescription = string.IsNullOrWhiteSpace(time)
                    ? description
                    : $"{time}{(string.IsNullOrWhiteSpace(description) ? string.Empty : " — " + description)}";

                db.Events.Add(new CalendarEvent
                {
                    Title = title,
                    EventDate = parsedDate,
                    Description = finalDescription
                });

                await db.SaveChangesAsync();

                return
                    $"Successfully added '{title}' to your calendar for {parsedDate:MMMM dd, yyyy}{(string.IsNullOrWhiteSpace(time) ? string.Empty : " at " + time)}.";
            }

            Console.WriteLine($"[AddToCalendar] Failed to parse: {cleanedDate}");
            return "I couldn't parse that date. Please provide it in a clearer format (e.g., 2026-05-01).";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AddToCalendar] Exception: {ex.Message}");
            return $"Error adding event: {ex.Message}";
        }
    }
}
