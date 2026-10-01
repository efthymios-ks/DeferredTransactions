namespace DeferredTransactions;

public interface IDeferredTransactionHandler<in TOperation>
    where TOperation : IDeferredTransactionOperation
{
    Task ApplyAsync(TOperation operation, CancellationToken cancellationToken = default);
}
