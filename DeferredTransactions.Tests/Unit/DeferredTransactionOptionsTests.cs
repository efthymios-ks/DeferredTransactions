using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeferredTransactions.Tests.Unit;

public sealed class DeferredTransactionOptionsTests
{
    [Fact]
    public void Defaults_WhenNotConfigured_ShouldMatchDocumentedValues()
    {
        // Arrange
        var options = new DeferredTransactionOptions();

        // Act
        var (defaultTtl, maxTtl, cleanupInterval, retention) =
            (options.DefaultTimeToLive, options.MaxTimeToLive, options.CleanupInterval, options.Retention);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), defaultTtl);
        Assert.Equal(TimeSpan.FromMinutes(30), maxTtl);
        Assert.Equal(TimeSpan.FromMinutes(1), cleanupInterval);
        Assert.Equal(TimeSpan.FromDays(7), retention);
    }

    [Fact]
    public void Validation_WhenIntervalIsZero_ShouldFailOnStart()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOptions<DeferredTransactionOptions>()
            .Configure(options => options.CleanupInterval = TimeSpan.Zero)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        using var provider = services.BuildServiceProvider();

        // Act
        void Act()
            => provider.GetRequiredService<IOptions<DeferredTransactionOptions>>().Value.GetHashCode();

        // Assert
        Assert.Throws<OptionsValidationException>(Act);
    }
}
