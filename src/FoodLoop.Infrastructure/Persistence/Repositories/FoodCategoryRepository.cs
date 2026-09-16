using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Persistence.Repositories;

public sealed class FoodCategoryRepository(ApplicationDbContext db)
    : Repository<FoodCategory>(db), IFoodCategoryRepository
{
    public async Task<IReadOnlyList<FoodCategory>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Context.FoodCategories.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
}
