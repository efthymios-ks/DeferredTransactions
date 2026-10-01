namespace DeferredTransactions.Tests.Unit;

public sealed class DeferredTransactionTests
{
    [Fact]
    public void IsCompleted_WhenNoTerminalTimestampIsSet_ShouldReturnFalse()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var transaction = new DeferredTransaction(Guid.NewGuid(), now, now.AddMinutes(5));

        // Act
        var completed = transaction.IsCompleted;

        // Assert
        Assert.False(completed);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void IsCompleted_WhenATerminalTimestampIsSet_ShouldReturnTrue(bool committed, bool rolledBack, bool expired)
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var transaction = new DeferredTransaction(
            Guid.NewGuid(),
            now,
            now.AddMinutes(5),
            CommittedAt: committed ? now : null,
            RolledBackAt: rolledBack ? now : null,
            ExpiredAt: expired ? now : null);

        // Act
        var completed = transaction.IsCompleted;

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public void IsActive_WhenNotCompletedAndNotExpired_ShouldReturnTrue()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var transaction = new DeferredTransaction(Guid.NewGuid(), now, now.AddMinutes(5));

        // Act
        var active = transaction.IsActive(now);

        // Assert
        Assert.True(active);
    }

    [Fact]
    public void IsActive_WhenExpirationHasPassed_ShouldReturnFalse()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var transaction = new DeferredTransaction(Guid.NewGuid(), now, now.AddMinutes(-1));

        // Act
        var active = transaction.IsActive(now);

        // Assert
        Assert.False(active);
    }

    [Fact]
    public void IsActive_WhenCompleted_ShouldReturnFalseEvenBeforeExpiration()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var transaction = new DeferredTransaction(
            Guid.NewGuid(), now, now.AddMinutes(5), CommittedAt: now);

        // Act
        var active = transaction.IsActive(now);

        // Assert
        Assert.False(active);
    }
}
