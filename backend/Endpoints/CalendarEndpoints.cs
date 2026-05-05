using Microsoft.EntityFrameworkCore;

/// <summary>
/// Maps calendar-related API endpoints.
/// </summary>
public static class CalendarEndpoints
{
    /// <summary>
    /// Maps the calendar endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/events", GetEventsAsync);
        endpoints.MapPost("/events", CreateEventAsync);
        endpoints.MapPut("/events/{id}", UpdateEventAsync);
        endpoints.MapDelete("/events/{id}", DeleteEventAsync);

        return endpoints;
    }

    private static async Task<IResult> GetEventsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var events = await db.Events
            .OrderBy(e => e.EventDate)
            .ToListAsync(cancellationToken);

        return Results.Ok(events);
    }

    private static async Task<IResult> CreateEventAsync(
        CalendarEventRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (!DateTime.TryParse(request.Date, out var parsedDate))
        {
            return Results.BadRequest("Invalid date");
        }

        var calendarEvent = new CalendarEvent
        {
            Title = request.Title,
            EventDate = parsedDate,
            Description = request.Description ?? string.Empty
        };

        db.Events.Add(calendarEvent);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(calendarEvent);
    }

    private static async Task<IResult> UpdateEventAsync(
        int id,
        CalendarEventRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await db.Events.FindAsync([id], cancellationToken);
        if (calendarEvent is null)
        {
            return Results.NotFound();
        }

        if (!DateTime.TryParse(request.Date, out var parsedDate))
        {
            return Results.BadRequest("Invalid date");
        }

        calendarEvent.Title = request.Title;
        calendarEvent.EventDate = parsedDate;
        calendarEvent.Description = request.Description ?? string.Empty;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(calendarEvent);
    }

    private static async Task<IResult> DeleteEventAsync(
        int id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await db.Events.FindAsync([id], cancellationToken);
        if (calendarEvent is null)
        {
            return Results.NotFound();
        }

        db.Events.Remove(calendarEvent);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok();
    }
}
