using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DeferredTransactions.Configuration;
using DeferredTransactions.Internal;

namespace DeferredTransactions;

public static class DependencyInjection
{
    public static IDeferredTransactionBuilder AddDeferredTransactions(
        this IServiceCollection services,
        Action<DeferredTransactionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<DeferredTransactionOptions>()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);

        var registrations = new OperationRegistrations();
        services.TryAddSingleton(registrations);
        services.TryAddSingleton<IOperationRegistry>(_ =>
            new OperationRegistry(registrations.OperationTypes, registrations.HandlerTypes, JsonDefaults.SerializerOptions));

        services.TryAddScoped<IDeferredTransactionManager, DeferredTransactionManager>();
        services.AddHostedService<DeferredTransactionCleanupService>();

        return new DeferredTransactionBuilder(services, registrations);
    }
}
