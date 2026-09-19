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
        services.AddScoped<OrganizationProfileService>();
        services.AddScoped<IDonationExpiryService, DonationExpiryService>();

        return services;
    }
}
