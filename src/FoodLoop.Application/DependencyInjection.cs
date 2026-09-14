using FoodLoop.Application.Admin;
using Microsoft.Extensions.DependencyInjection;
namespace FoodLoop.Application;
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAdminService, AdminService>();
        return services;
    }
}
