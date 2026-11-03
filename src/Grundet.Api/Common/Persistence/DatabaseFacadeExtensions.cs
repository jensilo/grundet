using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Grundet.Api.Common.Persistence;

/// <summary>
/// Shared persistence primitives for stores: transaction scoping and PostgreSQL advisory
/// locks. Kept as <see cref="DatabaseFacade"/> extensions so every store uses the same
/// commit/rollback and lock-key derivation without copying the boilerplate.
/// </summary>
public static class DatabaseFacadeExtensions
{
    public static async Task ExecuteInTransactionAsync(
        this DatabaseFacade database,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction = await database.BeginTransactionAsync(cancellationToken);
        try
        {
            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static async Task<T> ExecuteInTransactionAsync<T>(
        this DatabaseFacade database,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction = await database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Acquires a transaction-scoped PostgreSQL advisory lock derived from <paramref name="key"/>.
    /// Must be called inside a transaction (see <see cref="ExecuteInTransactionAsync(DatabaseFacade, Func{CancellationToken, Task}, CancellationToken)"/>);
    /// the lock releases automatically when that transaction ends.
    /// </summary>
    public static Task AcquireAdvisoryXactLockAsync(
        this DatabaseFacade database,
        Guid key,
        CancellationToken cancellationToken = default)
    {
        Span<byte> bytes = stackalloc byte[16];
        key.TryWriteBytes(bytes);
        int key1 = BitConverter.ToInt32(bytes[..4]);
        int key2 = BitConverter.ToInt32(bytes[4..8]);
        return database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({key1}, {key2})",
            cancellationToken);
    }
}
