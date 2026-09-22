using FoodLoop.Application.Identity;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Persistence;

/// <summary>
/// Creates the minimum operational accounts a fresh production environment needs.
/// Existing accounts are never modified, promoted, or given a new password.
/// </summary>
public sealed class ProductionBootstrapSeeder(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles)
{
    public async Task SeedAsync(
        string? adminEmail,
        string? adminPassword,
        string? courierEmail,
        string? courierPassword)
    {
        await CreateRequiredAccountAsync(adminEmail, adminPassword, AppRoles.Admin, "FoodLoop administrator");
        await CreateRequiredAccountAsync(courierEmail, courierPassword, AppRoles.Courier, "FoodLoop courier");
    }

    private async Task CreateRequiredAccountAsync(
        string? email,
        string? password,
        string role,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException($"Bootstrap credentials for {role} are required.");

        if (!await roles.RoleExistsAsync(role))
            throw new InvalidOperationException($"Required role '{role}' is not configured.");

        var existing = await users.FindByEmailAsync(email.Trim());
        if (existing is not null) return;

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email.Trim(),
            Email = email.Trim(),
            DisplayName = displayName,
            EmailConfirmed = true
        };

        Check(await users.CreateAsync(user, password));
        Check(await users.AddToRoleAsync(user, role));
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
