using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DeferredTransactions.Internal;

namespace DeferredTransactions.Configuration;

internal sealed class DeferredTransactionBuilder(
    IServiceCollection services,
    OperationRegistrations registrations
    ) : IDeferredTransactionBuilder
{
    private readonly OperationRegistrations _registrations = registrations;

    public IServiceCollection Services { get; } = services;

    public IDeferredTransactionBuilder WithStore<TStore>()
        where TStore : class, IDeferredTransactionStore
    {
        Services.RemoveAll<IDeferredTransactionStore>();
        Services.AddScoped<IDeferredTransactionStore, TStore>();

        return this;
    }

    public IDeferredTransactionBuilder WithHandler<TOperation, THandler>()
        where TOperation : IDeferredTransactionOperation
        where THandler : class, IDeferredTransactionHandler<TOperation>
    {
        Services.AddScoped<IDeferredTransactionHandler<TOperation>, THandler>();
        _registrations.OperationTypes.Add(typeof(TOperation));
        _registrations.HandlerTypes.Add(typeof(THandler));

        return this;
    }

    public IDeferredTransactionBuilder WithHandlersFromAssemblyOf<TAnchor>()
        => WithHandlersFromAssembly(typeof(TAnchor).Assembly);

    public IDeferredTransactionBuilder WithHandlersFromAssembly(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var operationTypes = OperationRegistry.DiscoverOperationTypes(assemblies);
        var handlerTypes = OperationRegistry.DiscoverHandlerTypes(assemblies);

        foreach (var operationType in operationTypes)
        {
            _registrations.OperationTypes.Add(operationType);
        }

        foreach (var handlerType in handlerTypes)
        {
            _registrations.HandlerTypes.Add(handlerType);

            foreach (var service in EnumerateHandlerServices(handlerType))
            {
                Services.AddScoped(service, handlerType);
            }
        }

        return this;
    }

    private static IEnumerable<Type> EnumerateHandlerServices(Type handlerType)
        => handlerType
            .GetInterfaces()
            .Where(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDeferredTransactionHandler<>));
}
