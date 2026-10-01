namespace DeferredTransactions;

public interface IDeferredTransactionManager
{
    Task<DeferredTransaction> BeginAsync(
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default
    );

    Task StageAsync<TOperation>(
        Guid transactionId,
        TOperation operation,
        CancellationToken cancellationToken = default
    )
        where TOperation : IDeferredTransactionOperation;

    Task CommitAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task RollbackAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<DeferredTransaction?> GetAsync(Guid transactionId, CancellationToken cancellationToken = default);
}
