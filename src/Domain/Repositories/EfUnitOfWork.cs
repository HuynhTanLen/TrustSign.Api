using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TrustSign.Common;
using TrustSign.Domain.IRepositories;

namespace TrustSign.Domain.Repositories;

internal sealed class EfUnitOfWork(TrustSignDbContext db) : IUnitOfWork
{
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancel)
        where TResult : Result
    {
        if (db.Database.CurrentTransaction is not null)
        {
            return await action();
        }

        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancel);
        try
        {
            TResult result = await action();
            if (result.IsSuccess)
            {
                await transaction.CommitAsync(cancel);
            }
            else
            {
                await transaction.RollbackAsync(cancel);
                db.ChangeTracker.Clear();
            }

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
