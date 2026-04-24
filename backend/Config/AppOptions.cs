public sealed class ChatModelOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434/v1";
    public string Model { get; set; } = "qwen2.5:7b";
    public string ApiKey { get; set; } = "test123";
    public string Instructions { get; set; } = string.Empty;
}

public sealed class EmbeddingModelOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434/v1";
    public string Model { get; set; } = "mxbai-embed-large";
    public int Dimensions { get; set; } = 1024;
    public string ApiKey { get; set; } = "test123";
}

public sealed class GoogleWorkspaceOptions
{
    public string CredentialsPath { get; set; } = "secrets/google-oauth-client.json";
    public string TokenDirectory { get; set; } = ".tokens/google-workspace";
    public string ApplicationName { get; set; } = "SpringAI2026 Email Organizer";
    public bool EnableGoogleCalendarWrite { get; set; }
    public string CalendarId { get; set; } = "primary";
}

public sealed class EmailProcessingOptions
{
    public string DefaultQuery { get; set; } = "in:inbox";
    public int DefaultSyncCount { get; set; } = 10;
    public bool ApplyGmailLabelsByDefault { get; set; } = true;
    public bool AddEventsToLocalCalendarByDefault { get; set; } = true;
    public bool AddEventsToGoogleCalendarByDefault { get; set; }
}
