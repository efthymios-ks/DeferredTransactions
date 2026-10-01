using Microsoft.Extensions.DependencyInjection;

namespace DeferredTransactions.Internal;

internal interface IOperationBinding
{
    string OperationType { get; }

    Type OperationClrType { get; }

    string Serialize(object operation);

    Task ApplyAsync(IServiceProvider services, string payload, CancellationToken cancellationToken = default);
}
