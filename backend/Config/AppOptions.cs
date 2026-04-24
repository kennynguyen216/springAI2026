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

public sealed class LocalMailboxOptions
{
    public string SampleDirectory { get; set; } = "SampleData/emails";
    public bool PersistCategoryLabels { get; set; } = true;
}

public sealed class EmailProcessingOptions
{
    public string DefaultQuery { get; set; } = string.Empty;
    public int DefaultSyncCount { get; set; } = 10;
    public bool ApplyLocalLabelsByDefault { get; set; } = true;
    public bool AddEventsToLocalCalendarByDefault { get; set; } = true;
}
