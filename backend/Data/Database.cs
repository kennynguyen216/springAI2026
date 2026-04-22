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
}