using Insaaf.Application.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
