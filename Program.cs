var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationFramework()
    .AddApplicationDatabase()
    .AddApplicationAi(builder.Configuration)
    .AddApplicationServices()
    .AddApplicationAgents();

var app = builder.Build();

app.UseFrontendAssets()
    .InitializeDatabase()
    .UseApplicationApi()
    .MapApplicationEndpoints();

app.Run();
