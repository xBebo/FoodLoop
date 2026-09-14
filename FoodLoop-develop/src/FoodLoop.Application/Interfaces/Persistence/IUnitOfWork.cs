namespace FoodLoop.Application.Interfaces.Persistence;
public interface IUnitOfWork
{
    // One call is atomic. Stage donation + claim + audit together before saving.
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
