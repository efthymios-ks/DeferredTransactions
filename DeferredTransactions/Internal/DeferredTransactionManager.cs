using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeferredTransactions.Internal;

internal sealed class DeferredTransactionManager(
    IDeferredTransactionStore store,
    IOperationRegistry registry,
    IServiceProvider services,
    IOptions<DeferredTransactionOptions> options,
    TimeProvider timeProvider
    ) : IDeferredTransactionManager
{
    private readonly IDeferredTransactionStore _store = store;
    private readonly IOperationRegistry _registry = registry;
    private readonly IServiceProvider _services = services;
    private readonly DeferredTransactionOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DeferredTransaction> BeginAsync(
        TimeSpan? timeToLive,
        CancellationToken cancellationToken
    )
    {
        var now = _timeProvider.GetUtcNow();
        var effective = timeToLive ?? _options.DefaultTimeToLive;
        if (effective > _options.MaxTimeToLive)
        {
            effective = _options.MaxTimeToLive;
        }

        var transaction = new DeferredTransaction(Guid.CreateVersion7(), now, now + effective);
        await _store.CreateAsync(transaction, cancellationToken);

        return transaction;
    }

    public async Task StageAsync<TOperation>(
        Guid transactionId,
        TOperation operation,
        CancellationToken cancellationToken
    )
        where TOperation : IDeferredTransactionOperation
    {
        ArgumentNullException.ThrowIfNull(operation);

        var binding = _registry.GetByClrType(typeof(TOperation));
        var payload = binding.Serialize(operation);

        await _store.AppendAsync(transactionId, binding.OperationType, payload, cancellationToken);
    }

    public Task CommitAsync(Guid transactionId, CancellationToken cancellationToken)
        => _store.CommitAsync(transactionId, ApplyEntriesAsync, cancellationToken);

    public Task RollbackAsync(Guid transactionId, CancellationToken cancellationToken)
        => _store.RollbackAsync(transactionId, cancellationToken);

    public Task<DeferredTransaction?> GetAsync(Guid transactionId, CancellationToken cancellationToken)
        => _store.GetAsync(transactionId, cancellationToken);

    private async Task ApplyEntriesAsync(IReadOnlyList<DeferredTransactionEntry> entries, CancellationToken cancellationToken)
    {
        foreach (var entry in entries.OrderBy(entry => entry.Sequence))
        {
            var binding = _registry.GetByOperationType(entry.OperationType);
            await binding.ApplyAsync(_services, entry.OperationPayload, cancellationToken);
        }
    }
}
