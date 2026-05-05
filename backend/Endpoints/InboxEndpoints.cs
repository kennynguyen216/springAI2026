/// <summary>
/// Maps inbox-processing API endpoints.
/// </summary>
public static class InboxEndpoints
{
    /// <summary>
    /// Maps the inbox scan endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapInboxEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/scan-inbox", HandleScanInboxAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleScanInboxAsync(
        InboxScannerService inboxScannerService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(InboxEndpoints));

        try
        {
            var response = await inboxScannerService.ScanAsync(cancellationToken);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scan inbox error");
            return Results.Problem(ex.Message);
        }
    }
}
