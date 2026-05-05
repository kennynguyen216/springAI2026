using System.Text.Json;

/// <summary>
/// Writes lightweight interaction audit records for Alfred requests.
/// </summary>
public sealed class AlfredInteractionLogger
{
    private readonly string _logFilePath;
    private readonly ILogger<AlfredInteractionLogger> _logger;
    private static readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="AlfredInteractionLogger"/> class.
    /// </summary>
    /// <param name="environment">The host environment.</param>
    /// <param name="logger">The logger instance.</param>
    public AlfredInteractionLogger(IHostEnvironment environment, ILogger<AlfredInteractionLogger> logger)
    {
        var logDirectory = Path.Combine(environment.ContentRootPath, "backend", "Logs");
        Directory.CreateDirectory(logDirectory);

        _logFilePath = Path.Combine(logDirectory, "alfred-interactions.jsonl");
        _logger = logger;
    }

    /// <summary>
    /// Appends a JSONL log entry for an Alfred interaction.
    /// </summary>
    /// <param name="userMessage">The original user message.</param>
    /// <param name="classification">The guardrail classification result.</param>
    /// <param name="response">The Alfred response when available.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public async Task LogAsync(string userMessage, string classification, string? response, CancellationToken cancellationToken = default)
    {
        var entry = new AlfredInteractionLogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            UserMessage = userMessage,
            Classification = classification,
            AlfredResponse = response
        };

        var json = JsonSerializer.Serialize(entry);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_logFilePath, json + Environment.NewLine, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write Alfred interaction log entry");
        }
        finally
        {
            _writeLock.Release();
        }
    }
}

/// <summary>
/// Represents one persisted Alfred interaction log record.
/// </summary>
public sealed class AlfredInteractionLogEntry
{
    /// <summary>
    /// Gets or sets the UTC timestamp for the interaction.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Gets or sets the original user message.
    /// </summary>
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guardrail classification result.
    /// </summary>
    public string Classification { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Alfred response, when one was produced.
    /// </summary>
    public string? AlfredResponse { get; set; }
}
