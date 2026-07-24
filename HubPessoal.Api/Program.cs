using Microsoft.EntityFrameworkCore;
using HubPessoal.Infrastructure;
using HubPessoal.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "database");

const string CorsPolicy = "frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    policy
    .WithOrigins(
        "http://localhost:3000",
        "https://frontend-hub-pessoal.vercel.app")
    .AllowAnyHeader()
    .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors(CorsPolicy);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
    .GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// app.MapGet("/", () => "Hub Pessoal API - v1.0.0");
app.MapHealthChecks("/health");
app.Run();
