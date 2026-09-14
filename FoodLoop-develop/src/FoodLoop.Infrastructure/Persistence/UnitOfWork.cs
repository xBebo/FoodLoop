using FoodLoop.Application.Exceptions;
using FoodLoop.Application.Interfaces.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace FoodLoop.Infrastructure.Persistence;
public sealed class UnitOfWork(ApplicationDbContext db) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException ex)
        { throw new PersistenceConflictException("The record changed. Reload and retry the operation.", ex); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new PersistenceConflictException("A record with this unique value already exists.", ex); }
    }
    public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => new ApplicationTransaction(await db.Database.BeginTransactionAsync(cancellationToken));
    private sealed class ApplicationTransaction(IDbContextTransaction transaction) : IApplicationTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
