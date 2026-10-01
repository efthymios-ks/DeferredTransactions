using System.Data;
using Microsoft.EntityFrameworkCore;

namespace DeferredTransactions.EntityFrameworkCore;

internal sealed class EntityFrameworkCoreDeferredTransactionStore(
    DbContext dbContext,
    TimeProvider timeProvider
    ) : IDeferredTransactionStore
{
    private readonly DbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task CreateAsync(DeferredTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        await _dbContext.Set<DeferredTransactionRecord>().AddAsync(new()
        {
            Id = transaction.Id,
            CreatedAt = transaction.CreatedAt,
            ExpiresAt = transaction.ExpiresAt,
            CommittedAt = transaction.CommittedAt,
            RolledBackAt = transaction.RolledBackAt,
            ExpiredAt = transaction.ExpiredAt,
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeferredTransaction?> GetAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Set<DeferredTransactionRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == transactionId, cancellationToken);

        return record?.ToDeferredTransaction();
    }

    public async Task AppendAsync(
        Guid transactionId,
        string operationType,
        string operationPayload,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
        ArgumentNullException.ThrowIfNull(operationPayload);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            _dbContext.ChangeTracker.Clear();

            var record = await LoadForUpdateAsync(transactionId, cancellationToken)
                ?? throw new DeferredTransactionNotFoundException(transactionId);
            if (!record.IsActive(_timeProvider.GetUtcNow()))
            {
                throw new DeferredTransactionNotActiveException(transactionId);
            }

            var nextSequence = await NextSequenceAsync(transactionId, cancellationToken);

            await _dbContext.Set<DeferredTransactionEntryRecord>().AddAsync(new()
            {
                TransactionId = transactionId,
                Sequence = nextSequence,
                OperationType = operationType,
                OperationPayload = operationPayload,
            }, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task CommitAsync(
        Guid transactionId,
        Func<IReadOnlyList<DeferredTransactionEntry>, CancellationToken, Task> applyEntries,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(applyEntries);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            _dbContext.ChangeTracker.Clear();

            var record = await LoadForUpdateAsync(transactionId, cancellationToken)
                ?? throw new DeferredTransactionNotFoundException(transactionId);
            if (!record.IsActive(_timeProvider.GetUtcNow()))
            {
                throw new DeferredTransactionNotActiveException(transactionId);
            }

            var entries = await _dbContext.Set<DeferredTransactionEntryRecord>()
                .AsNoTracking()
                .Where(entry => entry.TransactionId == transactionId)
                .OrderBy(entry => entry.Sequence)
                .Select(entry => entry.ToDeferredTransactionEntry())
                .ToArrayAsync(cancellationToken);

            await applyEntries(entries, cancellationToken);

            record.CommittedAt = _timeProvider.GetUtcNow();

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task RollbackAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            _dbContext.ChangeTracker.Clear();

            var record = await LoadForUpdateAsync(transactionId, cancellationToken)
                ?? throw new DeferredTransactionNotFoundException(transactionId);
            if (record.IsCompleted)
            {
                throw new DeferredTransactionNotActiveException(transactionId);
            }

            await _dbContext.Set<DeferredTransactionEntryRecord>()
                .Where(entry => entry.TransactionId == transactionId)
                .ExecuteDeleteAsync(cancellationToken);

            record.RolledBackAt = _timeProvider.GetUtcNow();

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task<int> ExpireAsync(DateTimeOffset currentTime, CancellationToken cancellationToken)
    {
        var expired = 0;
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            _dbContext.ChangeTracker.Clear();

            var stale = await _dbContext.Set<DeferredTransactionRecord>()
                .Where(record
                    => record.ExpiresAt <= currentTime
                    && record.CommittedAt == null
                    && record.RolledBackAt == null
                    && record.ExpiredAt == null
                )
                .ToArrayAsync(cancellationToken);

            if (stale.Length == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var ids = stale.Select(record => record.Id).ToArray();
            await _dbContext.Set<DeferredTransactionEntryRecord>()
                .Where(entry => ids.Contains(entry.TransactionId))
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var record in stale)
            {
                record.ExpiredAt = currentTime;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            expired = stale.Length;
        });

        return expired;
    }

    public async Task<int> PurgeAsync(DateTimeOffset completedBefore, CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();

        return await _dbContext.Set<DeferredTransactionRecord>()
            .Where(record
                => record.CommittedAt != null
                    && record.CommittedAt < completedBefore
                || record.RolledBackAt != null
                    && record.RolledBackAt < completedBefore
                || record.ExpiredAt != null
                    && record.ExpiredAt < completedBefore
            )
            .ExecuteDeleteAsync(cancellationToken);
    }

    private Task<DeferredTransactionRecord?> LoadForUpdateAsync(Guid transactionId, CancellationToken cancellationToken)
        => _dbContext.Set<DeferredTransactionRecord>()
            .FirstOrDefaultAsync(record => record.Id == transactionId, cancellationToken);

    private async Task<int> NextSequenceAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var last = await _dbContext.Set<DeferredTransactionEntryRecord>()
            .AsNoTracking()
            .Where(entry => entry.TransactionId == transactionId)
            .OrderByDescending(entry => entry.Sequence)
            .Select(entry => (int?)entry.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

        return (last ?? 0) + 1;
    }
}
