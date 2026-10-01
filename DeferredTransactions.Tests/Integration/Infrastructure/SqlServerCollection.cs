namespace DeferredTransactions.Tests.Integration.Infrastructure;

[CollectionDefinition(nameof(SqlServerCollection))]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}
