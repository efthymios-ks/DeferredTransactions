# DeferredTransactions

A .NET framework for API transactions that span multiple HTTP calls.

One call begins a transaction, any number of calls stage operations through whatever contracts the
app exposes, one call commits (all operations applied together in a single database transaction)
or rolls back. A background service expires stale transactions.

```
DeferredTransactions/                     core library — contracts, options, manager, registry, cleanup
├─ DeferredTransaction.cs                 the transaction record
├─ DeferredTransactionEntry.cs            one deferred operation as (Sequence, OperationType, Payload)
├─ DeferredTransactionOptions.cs          DefaultTimeToLive, MaxTimeToLive, CleanupInterval, Retention
├─ IDeferredTransactionOperation.cs       static abstract string OperationType
├─ IDeferredTransactionHandler<T>.cs      one handler per operation type
├─ IDeferredTransactionManager.cs         Begin / Stage / Commit / Rollback / Get
├─ IDeferredTransactionStore.cs           per-provider persistence contract
├─ DeferredTransactionNotFoundException   raised when an id is unknown
├─ DeferredTransactionNotActiveException  raised when staging or committing a completed / expired one
├─ DeferredTransactionRegistrationException raised at startup on a duplicate name or missing handler
├─ Configuration/                       IDeferredTransactionBuilder + DeferredTransactionBuilder
├─ Internal/                            OperationRegistry, OperationBinding<T>, manager, cleanup
└─ DependencyInjection.cs               AddDeferredTransactions

DeferredTransactions.EntityFrameworkCore/ EF Core store
├─ DeferredTransactionRecord.cs           transaction entity
├─ DeferredTransactionEntryRecord.cs      entry entity
├─ DeferredTransactionModelBuilderExtensions.cs  ConfigureDeferredTransactions
├─ EntityFrameworkCoreDeferredTransactionStore.cs the store, over Serializable + execution strategy
└─ DependencyInjection.cs               WithEntityFrameworkCoreStore<TDbContext>

DeferredTransactions.Tests/               xunit v3 + NSubstitute + FakeTimeProvider + Testcontainers.MsSql
├─ Unit/                                one test class per production class
└─ Integration/                         EF Core store against a real SQL Server container
```

## Design

- **No database transaction spans HTTP calls.** Operations are serialised into staging storage and
  replayed at commit inside one real database transaction.
- **Staging tables live in the target database.** Commit applies operations and marks the
  transaction committed atomically.
- **No status enum.** State is derived from nullable `DateTimeOffset` fields — `CommittedAt`,
  `RolledBackAt`, `ExpiredAt`. A transaction is completed when any is set.
- **TTL.** `BeginAsync(timeToLive)` computes `min(timeToLive ?? DefaultTimeToLive, MaxTimeToLive)`.
- **Background service.** Every `CleanupInterval` it calls `ExpireAsync` (marks and drains stale
  transactions) and `PurgeAsync` (deletes completed rows older than `Retention`). Runs inside its
  own DI scope.
- **Operations carry a stable wire name.** `static abstract string OperationType` on
  `IDeferredTransactionOperation`, so the stored type name never depends on a CLR name.
- **Deferred data is invisible to reads.** Handlers only run at commit.

## Registration

```csharp
services
    .AddDeferredTransactions(options =>
    {
        options.DefaultTimeToLive = TimeSpan.FromMinutes(10);
        options.MaxTimeToLive = TimeSpan.FromHours(1);
    })
    .WithEntityFrameworkCoreStore<ShopDbContext>()
    .WithHandlersFromAssemblyOf<CreateOrderHandler>();
```

| Method                                       | Effect                                                                              |
|----------------------------------------------|-------------------------------------------------------------------------------------|
| `AddDeferredTransactions(configure?)`          | Opens the chain, binds and validates `DeferredTransactionOptions`.                    |
| `WithStore<TStore>()`                        | Registers the persistence adapter (scoped).                                         |
| `WithEntityFrameworkCoreStore<TDbContext>()` | Sugar the EF Core package ships; equivalent to `WithStore` bound to `TDbContext`.   |
| `WithHandler<TOperation, THandler>()`        | Registers a single named handler and tracks the operation for the registry.         |
| `WithHandlersFromAssemblyOf<TAnchor>()`      | Scans the anchor's assembly for operations and handler implementations.             |
| `WithHandlersFromAssembly(params …)`         | Scans the given assemblies.                                                         |

Also register the entities on your `DbContext`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // your own entities …
    modelBuilder.ConfigureDeferredTransactions();
}
```

Generate a migration once the entities are on the context; the sample uses `EnsureCreatedAsync`
for a first run.

## Operations and handlers

```csharp
public sealed record CreateOrder(Guid OrderId, Guid CustomerId) : IDeferredTransactionOperation
{
    public static string OperationType => "orders.create";
}

public sealed class CreateOrderHandler(ShopDbContext dbContext) : IDeferredTransactionHandler<CreateOrder>
{
    public Task ApplyAsync(CreateOrder operation, CancellationToken cancellationToken)
    {
        dbContext.Orders.Add(new Order(operation.OrderId, operation.CustomerId));
        return Task.CompletedTask;
    }
}
```

Handlers run inside the real database transaction. They can read entities added earlier in the
same commit through `FindAsync` or the tracker.

## HTTP flow

```
POST   /api/deferred-transactions                 { "timeToLive": "00:15:00" }  → { id }
POST   /api/orders                              X-Deferred-Transaction-Id: {id} → { orderId }
POST   /api/orders/{orderId}/lines   (× N)      X-Deferred-Transaction-Id: {id}
POST   /api/deferred-transactions/{id}/commit     → all applied in one DB transaction
DELETE /api/deferred-transactions/{id}            → rollback
```

## Lifecycle

| Action   | Effect                                                                                  |
|----------|-----------------------------------------------------------------------------------------|
| Begin    | Creates a row with `CreatedAt` and `ExpiresAt`.                                         |
| Stage    | Appends an entry (`Sequence`, `OperationType`, `OperationPayload`) if the transaction is active. |
| Commit   | Serializable tx: load and lock row → load entries → run handlers → stamp `CommittedAt` → save. |
| Rollback | Sets `RolledBackAt`, deletes entries.                                                   |
| Expire   | Background: stamps `ExpiredAt` where `ExpiresAt <= now` and not completed; drains entries. |
| Purge    | Background: deletes completed rows older than `Retention`.                              |

## Options

| Option              | Default          | Notes                                                                     |
|---------------------|------------------|---------------------------------------------------------------------------|
| `DefaultTimeToLive` | `00:05:00`       | Used when `BeginAsync` gets no `timeToLive`.                              |
| `MaxTimeToLive`     | `00:30:00`       | Server-side ceiling; any `timeToLive` above it is capped.                 |
| `CleanupInterval`   | `00:01:00`       | How often the background service runs `ExpireAsync` and `PurgeAsync`.     |
| `Retention`         | `7.00:00:00`     | How long completed transactions are kept before `PurgeAsync` deletes them.|

Validated on start.

## Errors

| Exception                                  | Raised when                                                                    |
|--------------------------------------------|--------------------------------------------------------------------------------|
| `DeferredTransactionNotFoundException`       | An id passed to `Get`, `Stage`, `Commit` or `Rollback` doesn't exist.          |
| `DeferredTransactionNotActiveException`      | The transaction is committed, rolled back or expired when the caller acts.     |
| `DeferredTransactionRegistrationException`   | Startup: duplicate `OperationType`, an operation without a handler, or an unknown operation name at commit. |

Mapping these to HTTP status codes is left to the host.

## Tests

```powershell
dotnet test DeferredTransactions.Tests
```

Unit tests need nothing. Integration tests need Docker (Windows or Linux); one SQL Server 2022
container starts per run.

## License

MIT.
