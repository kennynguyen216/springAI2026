using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

public class ChatThread
{
    public string Id {get; set;} = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt {get; set;} = new();
}

public class ChatMessage
{
    public int Id {get; set;}
    public string ThreadId {get; set;} = string.Empty;
    public string Role {get; set;} = string.Empty;
    public string Content {get; set;} = string.Empty;
    public DateTime Timestamp {get; set;} = DateTime.UtcNow;
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ChatThread> Threads { get; set; }
    public DbSet<ChatMessage> Messages { get; set; }
    public DbSet<CalendarEvent> Events { get; set;}
    public DbSet<ProcessedEmail> ProcessedEmails { get; set; }
    public DbSet<EmailCalendarSuggestion> EmailCalendarSuggestions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProcessedEmail>()
            .HasIndex(email => email.GmailMessageId)
            .IsUnique();

        modelBuilder.Entity<ProcessedEmail>()
            .HasMany(email => email.CalendarSuggestions)
            .WithOne(suggestion => suggestion.ProcessedEmail)
            .HasForeignKey(suggestion => suggestion.ProcessedEmailId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CalendarEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Description { get; set; } = string.Empty;
}

public enum EmailCategory
{
    General = 0,
    Important = 1,
    Promotions = 2,
    Spam = 3
}

public class ProcessedEmail
{
    public int Id { get; set; }
    public string GmailMessageId { get; set; } = string.Empty;
    public string GmailThreadId { get; set; } = string.Empty;
    public string InternetMessageId { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string PlainTextBody { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ActionItemsJson { get; set; } = "[]";
    public string Category { get; set; } = EmailCategory.General.ToString();
    public string ClassificationReasoning { get; set; } = string.Empty;
    public string GmailLabelIdsJson { get; set; } = "[]";
    public string EmbeddingJson { get; set; } = "[]";
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    public bool LabelsApplied { get; set; }
    public bool HasCalendarSuggestions { get; set; }
    public List<EmailCalendarSuggestion> CalendarSuggestions { get; set; } = new();
}

public class EmailCalendarSuggestion
{
    public int Id { get; set; }
    public int ProcessedEmailId { get; set; }
    public ProcessedEmail? ProcessedEmail { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RawDateText { get; set; } = string.Empty;
    public string RawStartTimeText { get; set; } = string.Empty;
    public string RawEndTimeText { get; set; } = string.Empty;
    public bool IsAllDay { get; set; }
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public double Confidence { get; set; }
    public bool AddedToLocalCalendar { get; set; }
    public bool AddedToGoogleCalendar { get; set; }
}
