using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;

namespace DeferredTransactions.Internal;

internal sealed class OperationRegistry : IOperationRegistry
{
    private readonly FrozenDictionary<Type, IOperationBinding> _byClrType;
    private readonly FrozenDictionary<string, IOperationBinding> _byOperationType;

    public OperationRegistry(IEnumerable<Type> operationTypes, IEnumerable<Type> handlerTypes, JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(operationTypes);
        ArgumentNullException.ThrowIfNull(handlerTypes);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        var operations = operationTypes.Distinct().ToList();
        var handled = handlerTypes
            .SelectMany(handlerType => handlerType
                .GetInterfaces()
                .Where(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDeferredTransactionHandler<>))
                .Select(iface => iface.GetGenericArguments()[0]))
            .ToHashSet();

        var bindings = operations
            .Select(operationType => CreateBinding(operationType, serializerOptions))
            .ToList();

        EnsureUniqueOperationTypes(bindings);
        EnsureEveryOperationIsHandled(bindings, handled);

        _byClrType = bindings.ToFrozenDictionary(binding => binding.OperationClrType);
        _byOperationType = bindings.ToFrozenDictionary(binding => binding.OperationType, StringComparer.Ordinal);
    }

    public IOperationBinding GetByClrType(Type operationClrType)
        => _byClrType.TryGetValue(operationClrType, out var binding)
            ? binding
            : throw new DeferredTransactionRegistrationException(
                $"Operation type '{operationClrType.FullName}' is not registered. Call WithHandler or WithHandlersFromAssembly.");

    public IOperationBinding GetByOperationType(string operationType)
        => _byOperationType.TryGetValue(operationType, out var binding)
            ? binding
            : throw new DeferredTransactionRegistrationException(
                $"Operation '{operationType}' is not registered. Was its handler removed?");

    private static IOperationBinding CreateBinding(Type operationType, JsonSerializerOptions serializerOptions)
    {
        var bindingType = typeof(OperationBinding<>).MakeGenericType(operationType);
        return (IOperationBinding)Activator.CreateInstance(bindingType, serializerOptions)!;
    }

    private static void EnsureUniqueOperationTypes(IReadOnlyList<IOperationBinding> bindings)
    {
        var duplicate = bindings
            .GroupBy(binding => binding.OperationType, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is null)
        {
            return;
        }

        var names = string.Join(", ", duplicate.Select(binding => binding.OperationClrType.FullName));
        throw new DeferredTransactionRegistrationException(
            $"Duplicate operation type '{duplicate.Key}' on: {names}.");
    }

    private static void EnsureEveryOperationIsHandled(IReadOnlyList<IOperationBinding> bindings, HashSet<Type> handled)
    {
        var missing = bindings
            .Where(binding => !handled.Contains(binding.OperationClrType))
            .Select(binding => binding.OperationClrType.FullName)
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        throw new DeferredTransactionRegistrationException(
            $"No handler registered for: {string.Join(", ", missing)}.");
    }

    internal static IReadOnlyList<Type> DiscoverOperationTypes(IEnumerable<Assembly> assemblies)
        => [.. assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(IDeferredTransactionOperation).IsAssignableFrom(type))];

    internal static IReadOnlyList<Type> DiscoverHandlerTypes(IEnumerable<Assembly> assemblies)
        => [.. assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type.GetInterfaces().Any(iface =>
                iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDeferredTransactionHandler<>)))];
}
