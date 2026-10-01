using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using DeferredTransactions.Internal;
using DeferredTransactions.Tests.Unit.Support;

namespace DeferredTransactions.Tests.Unit.Internal;

public sealed class DeferredTransactionManagerTests
{
    private readonly FakeDeferredTransactionStore _store = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly DeferredTransactionOptions _options = new()
    {
        DefaultTimeToLive = TimeSpan.FromMinutes(5),
        MaxTimeToLive = TimeSpan.FromMinutes(30),
    };
    private readonly CounterState _state = new();

    [Fact]
    public async Task BeginAsync_WhenTimeToLiveIsOmitted_ShouldUseTheDefault()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        var transaction = await manager.BeginAsync(timeToLive: null, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(_time.GetUtcNow(), transaction.CreatedAt);
        Assert.Equal(_time.GetUtcNow() + _options.DefaultTimeToLive, transaction.ExpiresAt);
    }

    [Fact]
    public async Task BeginAsync_WhenTimeToLiveExceedsTheMax_ShouldCapAtTheMax()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        var transaction = await manager.BeginAsync(TimeSpan.FromDays(1), CancellationToken.None);

        // Assert
        Assert.Equal(_time.GetUtcNow() + _options.MaxTimeToLive, transaction.ExpiresAt);
    }

    [Fact]
    public async Task StageAsync_WhenOperationIsKnown_ShouldSerialiseAndAppend()
    {
        // Arrange
        var manager = CreateManager();
        var transaction = await manager.BeginAsync(timeToLive: null, cancellationToken: CancellationToken.None);

        // Act
        await manager.StageAsync(transaction.Id, new IncrementCounter(3), CancellationToken.None);

        // Assert
        Assert.DoesNotContain(transaction.Id, _store.Committed);
        Assert.Equal(0, _state.Total);
    }

    [Fact]
    public async Task CommitAsync_WhenEntriesArePresent_ShouldReplayThemInSequenceOrder()
    {
        // Arrange
        var manager = CreateManager();
        var transaction = await manager.BeginAsync(timeToLive: null, cancellationToken: CancellationToken.None);
        await manager.StageAsync(transaction.Id, new IncrementCounter(2), CancellationToken.None);
        await manager.StageAsync(transaction.Id, new IncrementCounter(3), CancellationToken.None);
        await manager.StageAsync(transaction.Id, new RecordName("first"), CancellationToken.None);

        // Act
        await manager.CommitAsync(transaction.Id, CancellationToken.None);

        // Assert
        Assert.Equal(5, _state.Total);
        Assert.Equal(["first"], _state.Names);
        Assert.Contains(transaction.Id, _store.Committed);
    }

    [Fact]
    public async Task RollbackAsync_WhenCalled_ShouldDelegateToTheStore()
    {
        // Arrange
        var manager = CreateManager();
        var transaction = await manager.BeginAsync(timeToLive: null, cancellationToken: CancellationToken.None);

        // Act
        await manager.RollbackAsync(transaction.Id, CancellationToken.None);

        // Assert
        Assert.Contains(transaction.Id, _store.RolledBack);
    }

    private DeferredTransactionManager CreateManager()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_state);
        services.AddScoped<IDeferredTransactionHandler<IncrementCounter>, IncrementCounterHandler>();
        services.AddScoped<IDeferredTransactionHandler<RecordName>, RecordNameHandler>();
        var provider = services.BuildServiceProvider();

        var registry = new OperationRegistry(
            [typeof(IncrementCounter), typeof(RecordName)],
            [typeof(IncrementCounterHandler), typeof(RecordNameHandler)],
            JsonDefaults.SerializerOptions);

        return new DeferredTransactionManager(_store, registry, provider, Options.Create(_options), _time);
    }
}
