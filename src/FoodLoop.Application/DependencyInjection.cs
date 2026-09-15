using FoodLoop.Application.Admin;
using FoodLoop.Application.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace FoodLoop.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ClaimService>();

        return services;
    }
}
