using System.ComponentModel;

/// <summary>
/// Provides weather-related tool functions exposed to agents.
/// </summary>
public static class WeatherTool
{
    private static readonly HttpClient HttpClient = new();

    /// <summary>
    /// Gets the current time and weather conditions for the requested location.
    /// </summary>
    /// <param name="location">The location to look up.</param>
    /// <returns>A string containing the local time and weather summary.</returns>
    [Description("Gets the current time and current weather conditions for a specific city or location.")]
    public static async Task<string> GetWeatherAndTime(string location)
    {
        var currentTime = DateTime.Now.ToString("h:mm tt on dddd, MMMM d, yyyy");

        try
        {
            var formattedLocation = location.Replace(" ", "+", StringComparison.Ordinal);
            var url = $"https://wttr.in/{formattedLocation}?format=3";
            var weatherReport = await HttpClient.GetStringAsync(url);

            return $"System Time: {currentTime}. Live Weather Report: {weatherReport.Trim()}";
        }
        catch (Exception ex)
        {
            return $"System Time: {currentTime}. Weather data unavailable: {ex.Message}";
        }
    }
}
