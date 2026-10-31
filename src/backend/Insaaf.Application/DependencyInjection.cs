using Insaaf.Application.Auth;
using Insaaf.Application.Restaurants;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        return services;
    }
}
