namespace DeferredTransactions;

public sealed class DeferredTransactionNotFoundException(Guid transactionId)
    : InvalidOperationException($"Deferred transaction {transactionId} was not found.")
{
    public Guid TransactionId { get; } = transactionId;
}
