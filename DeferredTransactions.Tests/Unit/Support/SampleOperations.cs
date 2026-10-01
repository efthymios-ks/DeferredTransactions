namespace DeferredTransactions.Tests.Unit.Support;

internal sealed record IncrementCounter(int Amount) : IDeferredTransactionOperation
{
    public static string OperationType => "test.increment";
}

internal sealed record RecordName(string Name) : IDeferredTransactionOperation
{
    public static string OperationType => "test.record-name";
}

internal sealed record UnhandledOperation : IDeferredTransactionOperation
{
    public static string OperationType => "test.unhandled";
}

internal sealed record DuplicateOperationA : IDeferredTransactionOperation
{
    public static string OperationType => "test.duplicate";
}

internal sealed record DuplicateOperationB : IDeferredTransactionOperation
{
    public static string OperationType => "test.duplicate";
}

internal sealed class CounterState
{
    public int Total { get; set; }

    public List<string> Names { get; } = [];
}

internal sealed class IncrementCounterHandler(CounterState state) : IDeferredTransactionHandler<IncrementCounter>
{
    public Task ApplyAsync(IncrementCounter operation, CancellationToken cancellationToken = default)
    {
        state.Total += operation.Amount;

        return Task.CompletedTask;
    }
}

internal sealed class RecordNameHandler(CounterState state) : IDeferredTransactionHandler<RecordName>
{
    public Task ApplyAsync(RecordName operation, CancellationToken cancellationToken = default)
    {
        state.Names.Add(operation.Name);

        return Task.CompletedTask;
    }
}
