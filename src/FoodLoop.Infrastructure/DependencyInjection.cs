Set - Content - Path "src/FoodLoop.Application/DependencyInjection.cs" - Value @"
using FoodLoop.Application.Courier;
using FoodLoop.Application.Donations;
using FoodLoop.Application.Organizations;
using Microsoft.Extensions.DependencyInjection;
 
namespace FoodLoop.Application;
 
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ClaimService>();
        services.AddScoped<CourierService>();
        services.AddScoped<OrganizationApprovalService>();
        services.AddScoped<OrganizationManagementService>();
        services.AddScoped<DonationService>();
        services.AddScoped<TaskDetailsService>();
        services.AddScoped<IDonationExpiryService, DonationExpiryService>();
 
        return services;
    }
}
"@
 