using Microsoft.EntityFrameworkCore;
using HubPessoal.Application;
using HubPessoal.Application.Interfaces;
using HubPessoal.Domain.Entities;
using HubPessoal.Infrastructure;
using HubPessoal.Infrastructure.Data;
using HubPessoal.Api.Middlewares;
using HubPessoal.Api.Contracts;
using HubPessoal.Application.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;

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

builder.Services.AddApplication();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]
                    ?? throw new InvalidOperationException("Jwt:Key not configured."))),
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
    .GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any())
    {
        var seedUsername = app.Configuration["Auth:SeedUsername"]
            ?? throw new InvalidOperationException("Auth:SeedUsername not configured.");
        var seedPassword = app.Configuration["Auth:SeedPassword"]
            ?? throw new InvalidOperationException("Auth:SeedPassword not configured.");

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.Users.Add(new User(seedUsername, passwordHasher.Hash(seedPassword)));
        db.SaveChanges();
    }
}

// app.MapGet("/", () => "Hub Pessoal API - v1.0.0");
app.MapHealthChecks("/health");

app.MapPost("/auth/login", async(LoginRequest request, AuthService authService) =>
{
    var token = await authService.LoginAsync(request.Username, request.Password);
    return token is null
        ? Results.Unauthorized()
        : Results.Ok(new LoginResponse(token));
});

app.MapGet("auth/me", (ClaimsPrincipal user) =>
{
    var username = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName);
    return Results.Ok(new { username });
}).RequireAuthorization();
// app.MapGet("/erro-teste", () =>
// {
//     throw new InvalidOperationException("Erro de teste do middleware.");
// });
app.Run();
