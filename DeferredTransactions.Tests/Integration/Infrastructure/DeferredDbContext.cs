using Microsoft.EntityFrameworkCore;
using DeferredTransactions.EntityFrameworkCore;

namespace DeferredTransactions.Tests.Integration.Infrastructure;

internal sealed class DeferredDbContext(DbContextOptions<DeferredDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureDeferredTransactions();
}
