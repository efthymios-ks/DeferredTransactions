namespace DeferredTransactions;

public sealed record DeferredTransactionEntry(int Sequence, string OperationType, string OperationPayload);
