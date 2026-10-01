using Microsoft.EntityFrameworkCore;

namespace DeferredTransactions.EntityFrameworkCore;

public static class DeferredTransactionModelBuilderExtensions
{
    public static ModelBuilder ConfigureDeferredTransactions(
        this ModelBuilder modelBuilder,
        string? schema = null
    )
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<DeferredTransactionRecord>(entity =>
        {
            entity.ToTable("DeferredTransactions", schema);

            entity.HasKey(record => record.Id);

            entity.Property(record => record.CreatedAt)
                .IsRequired();

            entity.Property(record => record.ExpiresAt)
                .IsRequired();

            entity.HasIndex(record => record.ExpiresAt);

            entity.HasMany(record => record.Entries)
                .WithOne()
                .HasForeignKey(entry => entry.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeferredTransactionEntryRecord>(entity =>
        {
            entity.ToTable("DeferredTransactionEntries", schema);

            entity.HasKey(entry => new
            {
                entry.TransactionId,
                entry.Sequence
            });

            entity.Property(entry => entry.OperationType)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(entry => entry.OperationPayload)
                .IsRequired();
        });

        return modelBuilder;
    }
}
