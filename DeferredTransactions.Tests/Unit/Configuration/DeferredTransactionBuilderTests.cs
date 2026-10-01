using Microsoft.Extensions.DependencyInjection;
using DeferredTransactions.Configuration;
using DeferredTransactions.Internal;
using DeferredTransactions.Tests.Unit.Support;

namespace DeferredTransactions.Tests.Unit.Configuration;

public sealed class DeferredTransactionBuilderTests
{
    [Fact]
    public void WithHandler_WhenCalled_ShouldRegisterHandlerAndTrackOperation()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrations = new OperationRegistrations();
        var builder = new DeferredTransactionBuilder(services, registrations);

        // Act
        builder.WithHandler<IncrementCounter, IncrementCounterHandler>();

        // Assert
        Assert.Contains(typeof(IncrementCounter), registrations.OperationTypes);
        Assert.Contains(typeof(IncrementCounterHandler), registrations.HandlerTypes);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IDeferredTransactionHandler<IncrementCounter>));
    }

    [Fact]
    public void WithHandlersFromAssemblyOf_WhenCalled_ShouldDiscoverEveryHandlerInThatAssembly()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrations = new OperationRegistrations();
        var builder = new DeferredTransactionBuilder(services, registrations);

        // Act
        builder.WithHandlersFromAssemblyOf<IncrementCounterHandler>();

        // Assert
        Assert.Contains(typeof(IncrementCounter), registrations.OperationTypes);
        Assert.Contains(typeof(RecordName), registrations.OperationTypes);
        Assert.Contains(typeof(IncrementCounterHandler), registrations.HandlerTypes);
        Assert.Contains(typeof(RecordNameHandler), registrations.HandlerTypes);
    }

    [Fact]
    public void WithStore_WhenCalled_ShouldReplaceAnyPreviousStoreRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IDeferredTransactionStore, FakeDeferredTransactionStore>();

        var registrations = new OperationRegistrations();
        var builder = new DeferredTransactionBuilder(services, registrations);

        // Act
        builder.WithStore<AnotherFakeStore>();

        // Assert
        var storeDescriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(IDeferredTransactionStore))
            .ToList();
        Assert.Single(storeDescriptors);
        Assert.Equal(typeof(AnotherFakeStore), storeDescriptors[0].ImplementationType);
    }

    private sealed class AnotherFakeStore : IDeferredTransactionStore
    {
        public Task CreateAsync(DeferredTransaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<DeferredTransaction?> GetAsync(Guid transactionId, CancellationToken cancellationToken = default)
            => Task.FromResult<DeferredTransaction?>(null);

        public Task AppendAsync(Guid transactionId, string operationType, string operationPayload, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CommitAsync(Guid transactionId, Func<IReadOnlyList<DeferredTransactionEntry>, CancellationToken, Task> applyEntries, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackAsync(Guid transactionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> ExpireAsync(DateTimeOffset currentTime, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> PurgeAsync(DateTimeOffset completedBefore, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
