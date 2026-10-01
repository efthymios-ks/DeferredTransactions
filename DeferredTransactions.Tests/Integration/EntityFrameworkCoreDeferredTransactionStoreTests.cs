using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using DeferredTransactions.EntityFrameworkCore;
using DeferredTransactions.Tests.Integration.Infrastructure;

namespace DeferredTransactions.Tests.Integration;

[Collection(nameof(SqlServerCollection))]
public sealed class EntityFrameworkCoreDeferredTransactionStoreTests(SqlServerFixture sqlServer)
    : IAsyncLifetime
{
    private readonly SqlServerFixture _sqlServer = sqlServer;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly string _databaseName = $"st-{Guid.NewGuid():N}";

    public async ValueTask InitializeAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    [Fact]
    public async Task AppendAsync_WhenTransactionIsActive_ShouldAssignAscendingSequenceNumbers()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var store = new EntityFrameworkCoreDeferredTransactionStore(dbContext, _time);
        var transaction = new DeferredTransaction(Guid.CreateVersion7(), _time.GetUtcNow(), _time.GetUtcNow().AddMinutes(5));
        await store.CreateAsync(transaction, CancellationToken.None);

        // Act
        await store.AppendAsync(transaction.Id, "op.one", "{}", CancellationToken.None);
        await store.AppendAsync(transaction.Id, "op.two", "{}", CancellationToken.None);

        // Assert
        var entries = await dbContext.Set<DeferredTransactionEntryRecord>()
            .Where(entry => entry.TransactionId == transaction.Id)
            .OrderBy(entry => entry.Sequence)
            .Select(entry => entry.Sequence)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal([1, 2], entries);
    }

    [Fact]
    public async Task AppendAsync_WhenTransactionHasExpired_ShouldThrow()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var store = new EntityFrameworkCoreDeferredTransactionStore(dbContext, _time);
        var transaction = new DeferredTransaction(Guid.CreateVersion7(), _time.GetUtcNow(), _time.GetUtcNow().AddMinutes(1));
        await store.CreateAsync(transaction, CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(2));

        // Act
        Task Act()
            => store.AppendAsync(transaction.Id, "op", "{}", CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<DeferredTransactionNotActiveException>(Act);
    }

    [Fact]
    public async Task CommitAsync_WhenCalled_ShouldReplayEntriesAndMarkCommitted()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var store = new EntityFrameworkCoreDeferredTransactionStore(dbContext, _time);
        var transaction = new DeferredTransaction(Guid.CreateVersion7(), _time.GetUtcNow(), _time.GetUtcNow().AddMinutes(5));
        await store.CreateAsync(transaction, CancellationToken.None);
        await store.AppendAsync(transaction.Id, "op.a", "{\"value\":1}", CancellationToken.None);
        await store.AppendAsync(transaction.Id, "op.b", "{\"value\":2}", CancellationToken.None);

        var replayed = new List<string>();

        // Act
        await store.CommitAsync(
            transaction.Id,
            (entries, _) =>
            {
                foreach (var entry in entries)
                {
                    replayed.Add(entry.OperationType);
                }

                return Task.CompletedTask;
            },
            CancellationToken.None);

        // Assert
        var reloaded = await store.GetAsync(transaction.Id, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded.CommittedAt);
        Assert.Equal(["op.a", "op.b"], replayed);
    }

    [Fact]
    public async Task RollbackAsync_WhenCalled_ShouldDeleteEntriesAndMarkRolledBack()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var store = new EntityFrameworkCoreDeferredTransactionStore(dbContext, _time);
        var transaction = new DeferredTransaction(Guid.CreateVersion7(), _time.GetUtcNow(), _time.GetUtcNow().AddMinutes(5));
        await store.CreateAsync(transaction, CancellationToken.None);
        await store.AppendAsync(transaction.Id, "op", "{}", CancellationToken.None);

        // Act
        await store.RollbackAsync(transaction.Id, CancellationToken.None);

        // Assert
        var reloaded = await store.GetAsync(transaction.Id, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded.RolledBackAt);
        var remaining = await dbContext.Set<DeferredTransactionEntryRecord>()
            .CountAsync(entry => entry.TransactionId == transaction.Id, TestContext.Current.CancellationToken);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task ExpireAsync_WhenTransactionsAreStale_ShouldMarkThemAndDeleteTheirEntries()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var store = new EntityFrameworkCoreDeferredTransactionStore(dbContext, _time);
        var transaction = new DeferredTransaction(Guid.CreateVersion7(), _time.GetUtcNow(), _time.GetUtcNow().AddMinutes(1));
        await store.CreateAsync(transaction, CancellationToken.None);
        await store.AppendAsync(transaction.Id, "op", "{}", CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(5));

        // Act
        var expired = await store.ExpireAsync(_time.GetUtcNow(), CancellationToken.None);

        // Assert
        Assert.Equal(1, expired);
        var reloaded = await store.GetAsync(transaction.Id, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded.ExpiredAt);
        var remaining = await dbContext.Set<DeferredTransactionEntryRecord>()
            .CountAsync(entry => entry.TransactionId == transaction.Id, TestContext.Current.CancellationToken);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task PurgeAsync_WhenCompletedTransactionsAreOlderThanRetention_ShouldDeleteThem()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var store = new EntityFrameworkCoreDeferredTransactionStore(dbContext, _time);
        var transaction = new DeferredTransaction(Guid.CreateVersion7(), _time.GetUtcNow(), _time.GetUtcNow().AddMinutes(5));
        await store.CreateAsync(transaction, CancellationToken.None);
        await store.CommitAsync(transaction.Id, (_, _) => Task.CompletedTask, CancellationToken.None);
        _time.Advance(TimeSpan.FromDays(2));

        // Act
        var purged = await store.PurgeAsync(_time.GetUtcNow(), CancellationToken.None);

        // Assert
        Assert.Equal(1, purged);
        var reloaded = await store.GetAsync(transaction.Id, CancellationToken.None);
        Assert.Null(reloaded);
    }

    private DeferredDbContext CreateDbContext()
    {
        var connectionString = $"{_sqlServer.ConnectionString};Database={_databaseName}";
        var options = new DbContextOptionsBuilder<DeferredDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;

        return new DeferredDbContext(options);
    }
}
