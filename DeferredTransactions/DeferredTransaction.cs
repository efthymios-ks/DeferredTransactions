namespace DeferredTransactions;

public sealed record DeferredTransaction(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? CommittedAt = null,
    DateTimeOffset? RolledBackAt = null,
    DateTimeOffset? ExpiredAt = null)
{
    public bool IsCompleted
        => CommittedAt is not null || RolledBackAt is not null || ExpiredAt is not null;

    public bool IsActive(DateTimeOffset currentTime)
        => !IsCompleted && ExpiresAt > currentTime;
}
