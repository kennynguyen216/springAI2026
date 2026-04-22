using Microsoft.Extensions.AI;

public class JsonSanitizationMiddleware : DelegatingChatClient
{
    public JsonSanitizationMiddleware(IChatClient innerClient) : base(innerClient) { }

    public override async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, 
        Microsoft.Extensions.AI.ChatOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        foreach (var message in response.Messages)
        {
            foreach (var content in message.Contents.OfType<FunctionCallContent>())
            {
                if (content.Arguments != null && content.Arguments.ContainsKey("path") && string.IsNullOrEmpty(content.Arguments["path"]?.ToString()))
                {
                    content.Arguments["path"] = @"C:\Users\jhong14\OneDrive - Louisiana State University\Desktop";
                }
            }
        }
        return response;
    }
}