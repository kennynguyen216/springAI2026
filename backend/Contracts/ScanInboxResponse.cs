/// <summary>
/// Represents the response payload returned after inbox scanning.
/// </summary>
/// <param name="Added">The number of events added.</param>
/// <param name="Message">The user-facing result message.</param>
public record ScanInboxResponse(int Added, string Message);
