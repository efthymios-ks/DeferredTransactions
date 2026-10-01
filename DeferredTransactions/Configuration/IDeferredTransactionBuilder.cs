using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace DeferredTransactions.Configuration;

public interface IDeferredTransactionBuilder
{
    IServiceCollection Services { get; }

    IDeferredTransactionBuilder WithStore<TStore>()
        where TStore : class, IDeferredTransactionStore;

    IDeferredTransactionBuilder WithHandler<TOperation, THandler>()
        where TOperation : IDeferredTransactionOperation
        where THandler : class, IDeferredTransactionHandler<TOperation>;

    IDeferredTransactionBuilder WithHandlersFromAssemblyOf<TAnchor>();

    IDeferredTransactionBuilder WithHandlersFromAssembly(params Assembly[] assemblies);
}
