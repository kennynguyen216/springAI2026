public static class EmailCategoryLabels
{
    public static IReadOnlyList<string> GetLabels(EmailCategory category)
    {
        return category switch
        {
            EmailCategory.Important => ["IMPORTANT", "INBOX"],
            EmailCategory.Promotions => ["PROMOTIONS", "INBOX"],
            EmailCategory.Spam => ["SPAM"],
            _ => ["INBOX"]
        };
    }
}

public static class EmailDateParser
{
    public static bool TryParseDateTime(string date, string time, bool allDay, out DateTime? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(date))
        {
            return false;
        }

        if (allDay || string.IsNullOrWhiteSpace(time))
        {
            if (!DateTime.TryParse(date, out var dayOnly))
            {
                return false;
            }

            parsed = DateTime.SpecifyKind(dayOnly.Date, DateTimeKind.Utc);
            return true;
        }

        if (!DateTime.TryParse($"{date} {time}", out var dateTime))
        {
            return false;
        }

        parsed = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return true;
    }
}
