namespace DeferredTransactions.Tests.Unit.Support;

internal sealed class FakeDeferredTransactionStore : IDeferredTransactionStore
{
    private readonly Dictionary<Guid, DeferredTransaction> _transactions = [];
    private readonly Dictionary<Guid, List<DeferredTransactionEntry>> _entries = [];

    public List<Guid> Committed { get; } = [];

    public List<Guid> RolledBack { get; } = [];

    public Task CreateAsync(DeferredTransaction transaction, CancellationToken cancellationToken)
    {
        _transactions[transaction.Id] = transaction;
        _entries[transaction.Id] = [];

        return Task.CompletedTask;
    }

    public Task<DeferredTransaction?> GetAsync(Guid transactionId, CancellationToken cancellationToken)
        => Task.FromResult(_transactions.TryGetValue(transactionId, out var transaction) ? transaction : null);

    public Task AppendAsync(Guid transactionId, string operationType, string operationPayload, CancellationToken cancellationToken)
    {
        if (!_entries.TryGetValue(transactionId, out var list))
        {
            throw new DeferredTransactionNotFoundException(transactionId);
        }

        list.Add(new DeferredTransactionEntry(list.Count + 1, operationType, operationPayload));

        return Task.CompletedTask;
    }

    public async Task CommitAsync(
        Guid transactionId,
        Func<IReadOnlyList<DeferredTransactionEntry>, CancellationToken, Task> applyEntries,
        CancellationToken cancellationToken
    )
    {
        var list = _entries[transactionId];
        await applyEntries(list, cancellationToken);
        Committed.Add(transactionId);
    }

    public Task RollbackAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        RolledBack.Add(transactionId);
        _entries[transactionId].Clear();

        return Task.CompletedTask;
    }

    public Task<int> ExpireAsync(DateTimeOffset currentTime, CancellationToken cancellationToken)
        => Task.FromResult(0);

    public Task<int> PurgeAsync(DateTimeOffset completedBefore, CancellationToken cancellationToken)
        => Task.FromResult(0);
}
