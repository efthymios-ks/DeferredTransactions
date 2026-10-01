using Testcontainers.MsSql;

namespace DeferredTransactions.Tests.Integration.Infrastructure;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString
        => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
        => await _container.StartAsync();

    public async ValueTask DisposeAsync()
        => await _container.DisposeAsync();
}
