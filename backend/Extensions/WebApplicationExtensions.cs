using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

/// <summary>
/// Provides grouped startup helpers for the web application pipeline.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures the frontend static-file pipeline when the frontend directory exists.
    /// </summary>
    /// <param name="app">The application being configured.</param>
    /// <returns>The same application instance for chaining.</returns>
    public static WebApplication UseFrontendAssets(this WebApplication app)
    {
        var frontendPath = Path.Combine(app.Environment.ContentRootPath, "frontend");
        if (!Directory.Exists(frontendPath))
        {
            return app;
        }

        var provider = new PhysicalFileProvider(frontendPath);

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = provider,
            OnPrepareResponse = context =>
            {
                // Frontend JS/CSS modules change frequently during local development,
                // so disable browser caching to avoid stale UI in external browsers.
                context.Context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
                context.Context.Response.Headers.Pragma = "no-cache";
                context.Context.Response.Headers.Expires = "0";
            }
        });

        return app;
    }

    /// <summary>
    /// Initializes the SQLite database on startup.
    /// </summary>
    /// <param name="app">The application being configured.</param>
    /// <returns>The same application instance for chaining.</returns>
    public static WebApplication InitializeDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Risk note: EnsureCreated is preserved to avoid changing startup behavior, even though migrations also exist.
        dbContext.Database.EnsureCreated();

        return app;
    }

    /// <summary>
    /// Maps middleware-like API features used by the application.
    /// </summary>
    /// <param name="app">The application being configured.</param>
    /// <returns>The same application instance for chaining.</returns>
    public static WebApplication UseApplicationApi(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options.WithTheme(ScalarTheme.DeepSpace));

        return app;
    }

    /// <summary>
    /// Maps all application endpoints.
    /// </summary>
    /// <param name="app">The application being configured.</param>
    /// <returns>The same application instance for chaining.</returns>
    public static WebApplication MapApplicationEndpoints(this WebApplication app)
    {
        app.MapChatEndpoints();
        app.MapInboxEndpoints();
        app.MapCalendarEndpoints();

        return app;
    }
}
