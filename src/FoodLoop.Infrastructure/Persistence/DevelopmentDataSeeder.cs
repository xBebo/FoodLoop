using FoodLoop.Application.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Infrastructure.Persistence;
public sealed class DevelopmentDataSeeder(ApplicationDbContext db, RoleManager<IdentityRole<Guid>> roles, UserManager<ApplicationUser> users)
{
    public async Task SeedAsync(string? password, CancellationToken cancellationToken = default)
    {
        foreach (var role in AppRoles.All)
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole<Guid>(role)));
        foreach (var name in new[] { "Prepared meals", "Produce", "Packaged food" })
            if (!await db.FoodCategories.AnyAsync(x => x.Name == name, cancellationToken)) db.FoodCategories.Add(new FoodCategory { Name = name });
        await db.SaveChangesAsync(cancellationToken);
        // User accounts are optional and use a password supplied privately by each developer.
        if (string.IsNullOrWhiteSpace(password)) return;
        var donor = await OrganizationAsync("DEMO-DONOR", "Demo donor", OrganizationType.Donor, OrganizationStatus.Active, cancellationToken);
        var beneficiary = await OrganizationAsync("DEMO-BEN-A", "Demo beneficiary A", OrganizationType.Beneficiary, OrganizationStatus.Active, cancellationToken);
        var beneficiaryB = await OrganizationAsync("DEMO-BEN-B", "Demo beneficiary B", OrganizationType.Beneficiary, OrganizationStatus.Active, cancellationToken);
        var pending = await OrganizationAsync("DEMO-PENDING", "Pending donor", OrganizationType.Donor, OrganizationStatus.Pending, cancellationToken);
        foreach (var account in new (string Name, string Role, Guid? OrganizationId)[] {
            ("admin", AppRoles.Admin, null), ("donor", AppRoles.Donor, donor.Id),
            ("beneficiary-a", AppRoles.Beneficiary, beneficiary.Id), ("beneficiary-b", AppRoles.Beneficiary, beneficiaryB.Id),
            ("courier", AppRoles.Courier, null), ("pending-donor", AppRoles.Donor, pending.Id) })
        {
            var email = account.Name + "@foodloop.test";
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email,
                    DisplayName = account.Name, EmailConfirmed = true, OrganizationId = account.OrganizationId };
                Check(await users.CreateAsync(user, password));
                Check(await users.AddToRoleAsync(user, account.Role));
            }
            // Never elevate or reset an existing account on a later seed run.
        }
    }
    private async Task<Organization> OrganizationAsync(string license, string name, OrganizationType type, OrganizationStatus status, CancellationToken ct)
    {
        var organization = await db.Organizations.SingleOrDefaultAsync(x => x.LicenseNumber == license, ct);
        if (organization is not null) return organization;
        organization = new Organization { LicenseNumber = license, Name = name, Type = type, Status = status, Address = "Demo address" };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);
        return organization;
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
