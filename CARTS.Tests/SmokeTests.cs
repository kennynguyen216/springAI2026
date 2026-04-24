using Xunit;
using System.Text.Json;

public class SmokeTests
{
    [Fact]
    public void EmailCategory_HasExpectedValues()
    {
        Assert.Equal("Important", EmailCategory.Important.ToString());
        Assert.Equal("Promotions", EmailCategory.Promotions.ToString());
        Assert.Equal("Spam", EmailCategory.Spam.ToString());
    }

    [Fact]
    public void EmailCategoryLabels_ReturnExpectedLocalLabels()
    {
        Assert.Equal(["IMPORTANT", "INBOX"], EmailCategoryLabels.GetLabels(EmailCategory.Important));
        Assert.Equal(["PROMOTIONS", "INBOX"], EmailCategoryLabels.GetLabels(EmailCategory.Promotions));
        Assert.Equal(["SPAM"], EmailCategoryLabels.GetLabels(EmailCategory.Spam));
    }

    [Fact]
    public void EmailDateParser_ParsesAllDayDates()
    {
        var success = EmailDateParser.TryParseDateTime("2026-05-01", string.Empty, true, out var parsed);

        Assert.True(success);
        Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), parsed);
    }

    [Fact]
    public async Task LocalMailboxReader_LoadsAndFiltersMessages()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"springAI2026-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var first = new LocalMailboxEmailDocument
            {
                Id = "email-1",
                Subject = "Exam reminder",
                FromAddress = "prof@example.edu",
                Snippet = "Final exam on Friday",
                PlainTextBody = "The final exam is on Friday at 9 AM.",
                Labels = ["INBOX"],
                ReceivedAtUtc = new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc)
            };

            var second = new LocalMailboxEmailDocument
            {
                Id = "email-2",
                Subject = "Coupon inside",
                FromAddress = "store@example.com",
                Snippet = "Save 20 percent",
                PlainTextBody = "Promo for this week only.",
                Labels = ["INBOX"],
                ReceivedAtUtc = new DateTime(2026, 4, 23, 12, 0, 0, DateTimeKind.Utc)
            };

            await File.WriteAllTextAsync(Path.Combine(tempDirectory, "1.json"), JsonSerializer.Serialize(first));
            await File.WriteAllTextAsync(Path.Combine(tempDirectory, "2.json"), JsonSerializer.Serialize(second));

            var result = await LocalMailboxReader.LoadAsync(
                tempDirectory,
                10,
                "exam",
                true,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                CancellationToken.None);

            var message = Assert.Single(result);
            Assert.Equal("email-1", message.GmailMessageId);
            Assert.Equal("Exam reminder", message.Subject);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }
}
