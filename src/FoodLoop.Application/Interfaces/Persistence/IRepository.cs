using FoodLoop.Domain.Common;
namespace FoodLoop.Application.Interfaces.Persistence;
public interface IRepository<T> where T : BaseEntity
{
    // Returns a tracked entity for use cases; callers must enforce ownership and authorization.
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(T entity);
}
