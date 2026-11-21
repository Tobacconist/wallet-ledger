using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallet.Core.Ledger;

namespace Wallet.Infrastructure.Persistence.Configurations;

internal sealed class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
    {
        builder.ToTable("ledger_transactions");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Type).HasConversion<short>();
        builder.Property(t => t.Description).HasMaxLength(LedgerTransaction.DescriptionMaxLength);

        builder.HasOne<LedgerTransaction>()
            .WithMany()
            .HasForeignKey(t => t.ReversesTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.ReversesTransactionId)
            .IsUnique()
            .HasFilter("reverses_transaction_id IS NOT NULL");

        builder.HasMany(t => t.Entries)
            .WithOne()
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(t => t.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
