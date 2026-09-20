using FoodLoop.Application.Interfaces.Auditing;
using FoodLoop.Application.Courier;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Infrastructure.Auditing;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace FoodLoop.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options => {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
        }).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.ConfigureApplicationCookie(options => {
            options.LoginPath = "/Auth/Login";
            options.AccessDeniedPath = "/Admin/AccessDenied";
        });
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IFoodDonationRepository, FoodDonationRepository>();
        services.AddScoped<IFoodCategoryRepository, FoodCategoryRepository>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IClaimDetailsReadRepository, ClaimDetailsReadRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAdminReadRepository, AdminReadRepository>();
        services.AddScoped<IOrganizationAdminReadRepository, OrganizationAdminReadRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ICourierRepository, CourierRepository>();
        services.AddScoped<ICourierTaskDetailsReadRepository, CourierTaskDetailsReadRepository>();
        services.AddScoped<ICourierDirectory, CourierDirectory>();
        services.AddScoped<DevelopmentDataSeeder>();
        return services;
    }
}
