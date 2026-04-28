using OpenAI;
using System.ClientModel;

public class SensitivityClassifier
{
    private readonly OpenAI.Chat.ChatClient _client;
    private readonly ILogger<SensitivityClassifier> _logger;

    private const string SystemPrompt = @"You are an email classification assistant. Classify each email into exactly one of three categories:

'sensitive' — The email contains PERSONAL private data belonging to the recipient, such as:
  - Their Social Security Number or government ID
  - Their passwords, PINs, or login credentials
  - Their personal bank account or credit card numbers
  - Their medical records or health diagnoses
  NOTE: Financial NEWS articles, stock market updates, and business news are NOT sensitive.

'relevant' — The email is personally addressed to the recipient and contains a specific event, appointment, invitation, or date that involves them personally.

'irrelevant' — The email is a newsletter, promotional email, mass mailing, news digest, or has no personal dates/events for the recipient.

Respond with ONLY one word: 'sensitive', 'relevant', or 'irrelevant'. No explanation. No punctuation.";

    public SensitivityClassifier(IConfiguration config, ILogger<SensitivityClassifier> logger)
    {
        var endpoint = config["Embeddings:Endpoint"] ?? "http://localhost:11434/v1";
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        _client = new OpenAIClient(new ApiKeyCredential("test123"), options).GetChatClient("llama3.2:1b");
        _logger = logger;
    }

    // Returns: "sensitive", "relevant", or "irrelevant"
    public async Task<string> ClassifyAsync(string emailContent)
    {
        try
        {
            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                OpenAI.Chat.ChatMessage.CreateSystemMessage(SystemPrompt),
                OpenAI.Chat.ChatMessage.CreateUserMessage($"Classify this email:\n\n{emailContent}")
            };

            var result = await _client.CompleteChatAsync(messages);
            var response = result.Value.Content[0].Text.Trim().ToLower();

            Console.WriteLine($"[CLASSIFIER] Result: {response}");

            if (response.StartsWith("sensitive")) return "sensitive";
            if (response.StartsWith("relevant")) return "relevant";
            return "irrelevant";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CLASSIFIER] Failed to classify — treating as sensitive by default");
            return "sensitive";
        }
    }
}
