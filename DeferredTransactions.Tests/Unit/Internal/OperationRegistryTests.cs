using DeferredTransactions.Internal;
using DeferredTransactions.Tests.Unit.Support;

namespace DeferredTransactions.Tests.Unit.Internal;

public sealed class OperationRegistryTests
{
    [Fact]
    public void GetByClrType_WhenOperationIsRegistered_ShouldReturnItsBinding()
    {
        // Arrange
        var registry = new OperationRegistry(
            [typeof(IncrementCounter)],
            [typeof(IncrementCounterHandler)],
            JsonDefaults.SerializerOptions);

        // Act
        var binding = registry.GetByClrType(typeof(IncrementCounter));

        // Assert
        Assert.Equal(IncrementCounter.OperationType, binding.OperationType);
        Assert.Equal(typeof(IncrementCounter), binding.OperationClrType);
    }

    [Fact]
    public void GetByOperationType_WhenNameIsRegistered_ShouldReturnItsBinding()
    {
        // Arrange
        var registry = new OperationRegistry(
            [typeof(IncrementCounter)],
            [typeof(IncrementCounterHandler)],
            JsonDefaults.SerializerOptions);

        // Act
        var binding = registry.GetByOperationType(IncrementCounter.OperationType);

        // Assert
        Assert.Equal(typeof(IncrementCounter), binding.OperationClrType);
    }

    [Fact]
    public void GetByClrType_WhenOperationIsNotRegistered_ShouldThrow()
    {
        // Arrange
        var registry = new OperationRegistry(
            [typeof(IncrementCounter)],
            [typeof(IncrementCounterHandler)],
            JsonDefaults.SerializerOptions);

        // Act
        void Act()
            => registry.GetByClrType(typeof(RecordName));

        // Assert
        Assert.Throws<DeferredTransactionRegistrationException>(Act);
    }

    [Fact]
    public void Construction_WhenTwoOperationsShareTheSameOperationType_ShouldThrow()
    {
        // Arrange
        var operations = new[] { typeof(DuplicateOperationA), typeof(DuplicateOperationB) };
        var handlers = Array.Empty<Type>();

        // Act
        void Act()
            => _ = new OperationRegistry(operations, handlers, JsonDefaults.SerializerOptions);

        // Assert
        var exception = Assert.Throws<DeferredTransactionRegistrationException>(Act);
        Assert.Contains("Duplicate operation type 'test.duplicate'", exception.Message);
    }

    [Fact]
    public void Construction_WhenAnOperationHasNoHandler_ShouldThrow()
    {
        // Arrange
        var operations = new[] { typeof(UnhandledOperation) };
        var handlers = Array.Empty<Type>();

        // Act
        void Act()
            => _ = new OperationRegistry(operations, handlers, JsonDefaults.SerializerOptions);

        // Assert
        var exception = Assert.Throws<DeferredTransactionRegistrationException>(Act);
        Assert.Contains("No handler registered", exception.Message);
    }
}
