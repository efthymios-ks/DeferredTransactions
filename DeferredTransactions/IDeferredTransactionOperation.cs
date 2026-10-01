namespace DeferredTransactions;

public interface IDeferredTransactionOperation
{
    static abstract string OperationType { get; }
}
