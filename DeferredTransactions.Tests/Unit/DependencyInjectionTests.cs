using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DeferredTransactions.EntityFrameworkCore;
using DeferredTransactions.Internal;
using DeferredTransactions.Tests.Unit.Support;

namespace DeferredTransactions.Tests.Unit;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddDeferredTransactions_WhenCalled_ShouldRegisterManagerAndCleanupService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<InMemoryDbContext>(dbOptions => dbOptions.UseInMemoryDatabase("st-di"));

        // Act
        services
            .AddDeferredTransactions()
            .WithEntityFrameworkCoreStore<InMemoryDbContext>()
            .WithHandler<IncrementCounter, IncrementCounterHandler>()
            .WithHandler<RecordName, RecordNameHandler>();
        services.AddSingleton(new CounterState());

        // Assert
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDeferredTransactionManager>());
        Assert.NotNull(provider.GetRequiredService<IOperationRegistry>());
        Assert.Single(
            provider.GetServices<IHostedService>().OfType<DeferredTransactionCleanupService>());
    }

    [Fact]
    public void WithEntityFrameworkCoreStore_WhenCalled_ShouldExposeTheStoreAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<InMemoryDbContext>(dbOptions => dbOptions.UseInMemoryDatabase("st-di-store"));
        services
            .AddDeferredTransactions()
            .WithEntityFrameworkCoreStore<InMemoryDbContext>()
            .WithHandler<IncrementCounter, IncrementCounterHandler>();
        services.AddSingleton(new CounterState());

        using var provider = services.BuildServiceProvider();

        // Act
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDeferredTransactionStore>();

        // Assert
        Assert.IsType<EntityFrameworkCoreDeferredTransactionStore>(store);
    }

    private sealed class InMemoryDbContext(DbContextOptions<InMemoryDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ConfigureDeferredTransactions();
    }
}
