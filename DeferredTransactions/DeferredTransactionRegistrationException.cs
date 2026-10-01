namespace DeferredTransactions;

public sealed class DeferredTransactionRegistrationException(string message) : InvalidOperationException(message)
{
}
