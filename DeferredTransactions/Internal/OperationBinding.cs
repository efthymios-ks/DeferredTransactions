using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace DeferredTransactions.Internal;

internal sealed class OperationBinding<TOperation>(JsonSerializerOptions serializerOptions) : IOperationBinding
    where TOperation : IDeferredTransactionOperation
{
    private readonly JsonSerializerOptions _serializerOptions = serializerOptions;

    public string OperationType { get; } = TOperation.OperationType;

    public Type OperationClrType { get; } = typeof(TOperation);

    public string Serialize(object operation)
        => JsonSerializer.Serialize((TOperation)operation, _serializerOptions);

    public Task ApplyAsync(IServiceProvider services, string payload, CancellationToken cancellationToken)
    {
        var operation = JsonSerializer.Deserialize<TOperation>(payload, _serializerOptions)
            ?? throw new InvalidOperationException($"Payload for '{TOperation.OperationType}' deserialized to null.");
        var handler = services.GetRequiredService<IDeferredTransactionHandler<TOperation>>();

        return handler.ApplyAsync(operation, cancellationToken);
    }
}
