namespace DeferredTransactions.EntityFrameworkCore;

internal sealed class DeferredTransactionEntryRecord
{
    public Guid TransactionId { get; set; }

    public int Sequence { get; set; }

    public string OperationType { get; set; } = string.Empty;

    public string OperationPayload { get; set; } = string.Empty;

    public DeferredTransactionEntry ToDeferredTransactionEntry()
        => new(Sequence, OperationType, OperationPayload);
}
