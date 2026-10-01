namespace DeferredTransactions.Internal;

internal sealed class OperationRegistrations
{
    public List<Type> OperationTypes { get; } = [];

    public List<Type> HandlerTypes { get; } = [];
}
