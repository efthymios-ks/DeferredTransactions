using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeferredTransactions.Internal;

internal sealed class DeferredTransactionCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<DeferredTransactionOptions> options,
    TimeProvider timeProvider,
    ILogger<DeferredTransactionCleanupService> logger
    ) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly DeferredTransactionOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<DeferredTransactionCleanupService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.CleanupInterval, _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Deferred transaction cleanup failed; will retry on the next tick.");
            }
        }
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDeferredTransactionStore>();
        var now = _timeProvider.GetUtcNow();

        var expired = await store.ExpireAsync(now, cancellationToken);
        var purged = await store.PurgeAsync(now - _options.Retention, cancellationToken);

        if (expired > 0 || purged > 0)
        {
            _logger.LogInformation("Deferred transaction cleanup: expired {Expired}, purged {Purged}.", expired, purged);
        }
    }
}
