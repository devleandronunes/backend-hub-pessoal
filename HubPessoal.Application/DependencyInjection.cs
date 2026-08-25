using HubPessoal.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HubPessoal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<NoteService>();
        services.AddScoped<NoteFolderService>();
        services.AddScoped<SyncService>();
        return services;
    }
}

