using Xunit;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

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
        Assert.Equal(new[] { "IMPORTANT", "INBOX" }, EmailCategoryLabels.GetLabels(EmailCategory.Important));
        Assert.Equal(new[] { "PROMOTIONS", "INBOX" }, EmailCategoryLabels.GetLabels(EmailCategory.Promotions));
        Assert.Equal(new[] { "SPAM" }, EmailCategoryLabels.GetLabels(EmailCategory.Spam));
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

    [Fact]
    public void DocumentQueryRouter_ExtractsMostRecentKeyword()
    {
        var matched = DocumentQueryRouter.TryGetMostRecentKeyword("show the most recent syllabus", out var keyword);

        Assert.True(matched);
        Assert.Equal("syllabus", keyword);
    }

    [Fact]
    public void LocalDocumentService_FindsMostRecentMatchingDocument()
    {
        var root = Path.Combine(Path.GetTempPath(), $"springAI2026-docs-{Guid.NewGuid():N}");
        var docs = Path.Combine(root, "Documents");
        var downloads = Path.Combine(root, "Downloads");
        Directory.CreateDirectory(docs);
        Directory.CreateDirectory(downloads);

        try
        {
            var older = Path.Combine(docs, "cs101-syllabus.txt");
            var newer = Path.Combine(downloads, "spring-syllabus.md");
            File.WriteAllText(older, "older");
            File.WriteAllText(newer, "newer");
            File.SetLastWriteTimeUtc(older, new DateTime(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newer, new DateTime(2026, 4, 24, 8, 0, 0, DateTimeKind.Utc));

            var service = new LocalDocumentService(new TestEnvironment(), [root, docs, downloads]);
            var result = service.FindRecentDocuments("syllabus", 5);

            Assert.Equal(2, result.Count);
            Assert.Equal("spring-syllabus.md", result[0].Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void LocalDocumentService_ResolvesBareFilenameToRecentMatch()
    {
        var root = Path.Combine(Path.GetTempPath(), $"springAI2026-doc-read-{Guid.NewGuid():N}");
        var docs = Path.Combine(root, "Documents");
        Directory.CreateDirectory(docs);

        try
        {
            var filePath = Path.Combine(docs, "ethics_essay.txt");
            File.WriteAllText(filePath, "Ethics essay content.");
            File.SetLastWriteTimeUtc(filePath, new DateTime(2026, 4, 24, 9, 0, 0, DateTimeKind.Utc));

            var service = new LocalDocumentService(new TestEnvironment(), [root, docs]);

            var resolved = service.ResolveDocumentPath("ethics_essay.txt");
            var text = service.ReadDocumentText("ethics_essay.txt");

            Assert.Equal(filePath, resolved);
            Assert.Equal("Ethics essay content.", text);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CARTS.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
