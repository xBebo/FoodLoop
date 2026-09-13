using FoodLoop.Application.Interfaces.Persistence;
using FoodLoop.Domain.Common;
namespace FoodLoop.Infrastructure.Persistence.Repositories;
public class Repository<T>(ApplicationDbContext db) : IRepository<T> where T : BaseEntity
{
    protected ApplicationDbContext Context { get; } = db;
    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Context.Set<T>().FindAsync([id], cancellationToken).AsTask();
    public void Add(T entity) => Context.Set<T>().Add(entity);
}
