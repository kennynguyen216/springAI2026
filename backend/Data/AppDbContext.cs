using Microsoft.EntityFrameworkCore;

/// <summary>
/// Entity Framework database context for the application.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppDbContext"/> class.
    /// </summary>
    /// <param name="options">The configured database options.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the stored conversation threads.
    /// </summary>
    public DbSet<ChatThread> Threads { get; set; }

    /// <summary>
    /// Gets or sets the stored chat messages.
    /// </summary>
    public DbSet<ChatMessage> Messages { get; set; }

    /// <summary>
    /// Gets or sets the stored calendar events.
    /// </summary>
    public DbSet<CalendarEvent> Events { get; set; }
}
