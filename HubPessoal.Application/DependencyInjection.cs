using HubPessoal.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HubPessoal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        return services;
    }
}

