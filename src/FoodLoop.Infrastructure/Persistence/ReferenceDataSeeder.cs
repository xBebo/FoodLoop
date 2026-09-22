using FoodLoop.Application.Identity;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Persistence;

/// <summary>
/// Creates application reference data that every environment requires.
/// This is intentionally separate from demo-account seeding so it is safe to run in Production.
/// </summary>
public sealed class ReferenceDataSeeder(
    ApplicationDbContext db,
    RoleManager<IdentityRole<Guid>> roles)
{
    private static readonly string[] CategoryNames = ["Prepared meals", "Produce", "Packaged food"];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in AppRoles.All)
        {
            if (await roles.RoleExistsAsync(role)) continue;
            Check(await roles.CreateAsync(new IdentityRole<Guid>(role)));
        }

        foreach (var name in CategoryNames)
        {
            if (await db.FoodCategories.AnyAsync(x => x.Name == name, cancellationToken)) continue;
            db.FoodCategories.Add(new FoodCategory { Name = name });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
