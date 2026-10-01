namespace DeferredTransactions;

public sealed class DeferredTransactionNotActiveException(Guid transactionId)
    : InvalidOperationException($"Deferred transaction {transactionId} is not active.")
{
    public Guid TransactionId { get; } = transactionId;
}
