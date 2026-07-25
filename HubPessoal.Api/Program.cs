using Microsoft.EntityFrameworkCore;
using HubPessoal.Infrastructure;
using HubPessoal.Infrastructure.Data;
using HubPessoal.Api.Middlewares;

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

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

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
    .GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// app.MapGet("/", () => "Hub Pessoal API - v1.0.0");
app.MapHealthChecks("/health");
// app.MapGet("/erro-teste", () =>
// {
//     throw new InvalidOperationException("Erro de teste do middleware.");
// });
app.Run();
