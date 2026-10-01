using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using DeferredTransactions.Internal;

namespace DeferredTransactions.Tests.Unit.Internal;

public sealed class DeferredTransactionCleanupServiceTests
{
    [Fact]
    public async Task RunOnceAsync_WhenCalled_ShouldCallExpireThenPurgeWithTheRetentionWindow()
    {
        // Arrange
        var store = Substitute.For<IDeferredTransactionStore>();
        store.ExpireAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(1);
        store.PurgeAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(2);

        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();

        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var options = Options.Create(new DeferredTransactionOptions { Retention = TimeSpan.FromDays(3) });
        var service = new DeferredTransactionCleanupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            time,
            NullLogger<DeferredTransactionCleanupService>.Instance);

        // Act
        await service.RunOnceAsync(CancellationToken.None);

        // Assert
        await store.Received(1).ExpireAsync(time.GetUtcNow(), Arg.Any<CancellationToken>());
        await store.Received(1).PurgeAsync(time.GetUtcNow() - TimeSpan.FromDays(3), Arg.Any<CancellationToken>());
    }
}
