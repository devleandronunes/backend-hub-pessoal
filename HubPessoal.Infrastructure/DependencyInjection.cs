using HubPessoal.Application.Interfaces;
using HubPessoal.Infrastructure.Data;
using HubPessoal.Infrastructure.Data.Repositories;
using HubPessoal.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HubPessoal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<INoteFolderRepository, NoteFolderRepository>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }
}