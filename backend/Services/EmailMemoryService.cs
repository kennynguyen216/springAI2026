using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

public sealed class EmailMemoryService
{
    private readonly AppDbContext _dbContext;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public EmailMemoryService(
        AppDbContext dbContext,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _dbContext = dbContext;
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task<List<ProcessedEmailSearchHit>> SearchAsync(string query, int top = 5, CancellationToken cancellationToken = default)
    {
        var emails = await _dbContext.ProcessedEmails
            .AsNoTracking()
            .OrderByDescending(email => email.ProcessedAtUtc)
            .ToListAsync(cancellationToken);

        if (emails.Count == 0)
        {
            return new List<ProcessedEmailSearchHit>();
        }

        var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);
        if (queryEmbedding.Length == 0)
        {
            return emails
                .Where(email =>
                    email.Subject.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    email.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    email.PlainTextBody.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(top)
                .Select(email => new ProcessedEmailSearchHit(email.Id, email.Subject, email.Summary, email.Category, 0.5))
                .ToList();
        }

        return emails
            .Select(email => new
            {
                Email = email,
                Score = CosineSimilarity(queryEmbedding, DeserializeEmbedding(email.EmbeddingJson))
            })
            .OrderByDescending(result => result.Score)
            .Take(top)
            .Select(result => new ProcessedEmailSearchHit(
                result.Email.Id,
                result.Email.Subject,
                result.Email.Summary,
                result.Email.Category,
                result.Score))
            .ToList();
    }

    public async Task<string> CreateEmbeddingJsonAsync(string text, CancellationToken cancellationToken = default)
    {
        var embedding = await GenerateEmbeddingAsync(text, cancellationToken);
        return JsonSerializer.Serialize(embedding, _jsonOptions);
    }

    public async Task UpsertProcessedEmailAsync(ProcessedEmail email, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ProcessedEmails
            .Include(item => item.CalendarSuggestions)
            .FirstOrDefaultAsync(item => item.GmailMessageId == email.GmailMessageId, cancellationToken);

        if (existing is null)
        {
            _dbContext.ProcessedEmails.Add(email);
        }
        else
        {
            existing.GmailThreadId = email.GmailThreadId;
            existing.InternetMessageId = email.InternetMessageId;
            existing.FromAddress = email.FromAddress;
            existing.Subject = email.Subject;
            existing.Snippet = email.Snippet;
            existing.PlainTextBody = email.PlainTextBody;
            existing.Summary = email.Summary;
            existing.ActionItemsJson = email.ActionItemsJson;
            existing.Category = email.Category;
            existing.ClassificationReasoning = email.ClassificationReasoning;
            existing.GmailLabelIdsJson = email.GmailLabelIdsJson;
            existing.EmbeddingJson = email.EmbeddingJson;
            existing.ReceivedAtUtc = email.ReceivedAtUtc;
            existing.ProcessedAtUtc = email.ProcessedAtUtc;
            existing.LabelsApplied = email.LabelsApplied;
            existing.HasCalendarSuggestions = email.HasCalendarSuggestions;

            _dbContext.EmailCalendarSuggestions.RemoveRange(existing.CalendarSuggestions);
            existing.CalendarSuggestions.Clear();
            foreach (var suggestion in email.CalendarSuggestions)
            {
                existing.CalendarSuggestions.Add(suggestion);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<float>();
        }

        try
        {
            var vector = await _embeddingGenerator.GenerateVectorAsync(text, cancellationToken: cancellationToken);
            return vector.ToArray();
        }
        catch
        {
            return Array.Empty<float>();
        }
    }

    private float[] DeserializeEmbedding(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<float[]>(json, _jsonOptions) ?? Array.Empty<float>();
        }
        catch
        {
            return Array.Empty<float>();
        }
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length == 0 || right.Length == 0 || left.Length != right.Length)
        {
            return 0;
        }

        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftMagnitude += left[i] * left[i];
            rightMagnitude += right[i] * right[i];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}

public sealed record ProcessedEmailSearchHit(
    int Id,
    string Subject,
    string Summary,
    string Category,
    double Score);
