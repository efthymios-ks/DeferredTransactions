namespace DeferredTransactions.EntityFrameworkCore;

internal sealed class DeferredTransactionRecord
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? CommittedAt { get; set; }

    public DateTimeOffset? RolledBackAt { get; set; }

    public DateTimeOffset? ExpiredAt { get; set; }

    public List<DeferredTransactionEntryRecord> Entries { get; set; } = [];

    public DeferredTransaction ToDeferredTransaction()
        => new(Id, CreatedAt, ExpiresAt, CommittedAt, RolledBackAt, ExpiredAt);

    public bool IsCompleted
        => CommittedAt is not null || RolledBackAt is not null || ExpiredAt is not null;

    public bool IsActive(DateTimeOffset currentTime)
        => !IsCompleted && ExpiresAt > currentTime;
}
