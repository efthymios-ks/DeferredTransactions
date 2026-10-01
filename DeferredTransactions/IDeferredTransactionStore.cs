namespace DeferredTransactions;

public interface IDeferredTransactionStore
{
    Task CreateAsync(DeferredTransaction transaction, CancellationToken cancellationToken = default);

    Task<DeferredTransaction?> GetAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task AppendAsync(
        Guid transactionId,
        string operationType,
        string operationPayload,
        CancellationToken cancellationToken = default
    );

    Task CommitAsync(
        Guid transactionId,
        Func<IReadOnlyList<DeferredTransactionEntry>, CancellationToken, Task> applyEntries,
        CancellationToken cancellationToken = default
    );

    Task RollbackAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<int> ExpireAsync(DateTimeOffset currentTime, CancellationToken cancellationToken = default);

    Task<int> PurgeAsync(DateTimeOffset completedBefore, CancellationToken cancellationToken = default);
}
