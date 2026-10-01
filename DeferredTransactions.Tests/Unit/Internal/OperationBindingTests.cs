using Microsoft.Extensions.DependencyInjection;
using DeferredTransactions.Internal;
using DeferredTransactions.Tests.Unit.Support;

namespace DeferredTransactions.Tests.Unit.Internal;

public sealed class OperationBindingTests
{
    [Fact]
    public void Serialize_WhenGivenAnOperation_ShouldRoundTripToTheSameJson()
    {
        // Arrange
        var binding = new OperationBinding<IncrementCounter>(JsonDefaults.SerializerOptions);
        var operation = new IncrementCounter(5);

        // Act
        var payload = binding.Serialize(operation);

        // Assert
        Assert.Contains("\"amount\":5", payload);
    }

    [Fact]
    public async Task ApplyAsync_WhenHandlerIsResolved_ShouldRunItWithTheDeserialisedOperation()
    {
        // Arrange
        var state = new CounterState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddScoped<IDeferredTransactionHandler<IncrementCounter>, IncrementCounterHandler>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var binding = new OperationBinding<IncrementCounter>(JsonDefaults.SerializerOptions);
        var payload = binding.Serialize(new IncrementCounter(4));

        // Act
        await binding.ApplyAsync(scope.ServiceProvider, payload, CancellationToken.None);

        // Assert
        Assert.Equal(4, state.Total);
    }
}
