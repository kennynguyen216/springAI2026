using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

/// <summary>
/// Provides grouped service-registration helpers for application startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers framework services used by the application.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddApplicationFramework(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddOpenApi();

        return services;
    }

    /// <summary>
    /// Registers the Entity Framework database layer.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddApplicationDatabase(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=NewAgentDB.db"));

        services.AddSingleton<AgentSessionManager>();

        return services;
    }

    /// <summary>
    /// Registers the chat client used by Alfred and related services.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddApplicationAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IChatClient>(_ =>
        {
            var options = new OpenAIClientOptions
            {
                // Risk note: this preserves the current behavior and still assumes Chat:Endpoint exists.
                Endpoint = new Uri(configuration["Chat:Endpoint"])
            };

            var model = configuration["Chat:Model"];
            var client = new OpenAIClient(new ApiKeyCredential("test123"), options);

            return client.GetChatClient(model).AsIChatClient();
        });

        return services;
    }

    /// <summary>
    /// Registers application services and guardrail layers.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<SensitivityClassifier>();
        services.AddSingleton<AlfredInteractionLogger>();
        services.AddScoped<AlfredInputGuardrail>();
        services.AddScoped<AlfredOutputGuardrail>();
        services.AddScoped<AlfredChatService>();
        services.AddScoped<InboxScannerService>();

        return services;
    }

    /// <summary>
    /// Registers the agent definitions used by the application.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddApplicationAgents(this IServiceCollection services)
    {
        services.AddAlfredAgent();
        services.AddEmailAgent();
        services.AddCalendarAgent();

        return services;
    }
}
