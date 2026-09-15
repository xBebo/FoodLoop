using FoodLoop.Domain.Entities;

namespace FoodLoop.Application.Interfaces.Persistence;

public interface IFoodCategoryRepository : IRepository<FoodCategory>
{
    Task<IReadOnlyList<FoodCategory>> GetAllAsync(CancellationToken cancellationToken = default);
}
