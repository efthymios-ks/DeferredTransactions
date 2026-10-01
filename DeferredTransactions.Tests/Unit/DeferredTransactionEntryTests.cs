namespace DeferredTransactions.Tests.Unit;

public sealed class DeferredTransactionEntryTests
{
    [Fact]
    public void Constructor_WhenValuesArePassed_ShouldExposeThem()
    {
        // Arrange
        var entry = new DeferredTransactionEntry(3, "orders.create", "{\"OrderId\":\"1\"}");

        // Act
        var (sequence, operationType, operationPayload) = (entry.Sequence, entry.OperationType, entry.OperationPayload);

        // Assert
        Assert.Equal(3, sequence);
        Assert.Equal("orders.create", operationType);
        Assert.Equal("{\"OrderId\":\"1\"}", operationPayload);
    }
}
