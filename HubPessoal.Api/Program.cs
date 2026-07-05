using HubPessoal.Infrastructure;
using HubPessoal.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "database");

var app = builder.Build();

// app.MapGet("/", () => "Hub Pessoal API - v1.0.0");
app.MapHealthChecks("/health");

app.Run();
