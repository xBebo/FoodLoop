using FoodLoop.Application.Admin;
using FoodLoop.Application.Courier;
using FoodLoop.Application.Organizations;
using FoodLoop.Application.Donations;
using FoodLoop.Application.Claims;
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
<<<<<<< HEAD
        services.AddScoped<IDonationExpiryService, DonationExpiryService>();

=======
        services.AddScoped<TaskDetailsService>();
>>>>>>> 644ab77b0285a989af9f1f3e073c2750db253a4c
        return services;
    }
}
