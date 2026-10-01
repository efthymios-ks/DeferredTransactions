using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DeferredTransactions.Configuration;

namespace DeferredTransactions.EntityFrameworkCore;

public static class DependencyInjection
{
    public static IDeferredTransactionBuilder WithEntityFrameworkCoreStore<TDbContext>(this IDeferredTransactionBuilder builder)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.RemoveAll<IDeferredTransactionStore>();
        builder.Services.AddScoped<IDeferredTransactionStore>(serviceProvider => new EntityFrameworkCoreDeferredTransactionStore(
            serviceProvider.GetRequiredService<TDbContext>(),
            serviceProvider.GetRequiredService<TimeProvider>()
        ));

        return builder;
    }
}
