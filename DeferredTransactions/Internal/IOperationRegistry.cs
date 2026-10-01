namespace DeferredTransactions.Internal;

internal interface IOperationRegistry
{
    IOperationBinding GetByClrType(Type operationClrType);

    IOperationBinding GetByOperationType(string operationType);
}
