using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallet.Core.Accounts;
using Wallet.Core.Ledger;

namespace Wallet.Infrastructure.Persistence.Configurations;

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries", t =>
        {
            t.HasCheckConstraint("ck_ledger_entries_amount_non_zero", "amount <> 0");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityByDefaultColumn();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Statement queries page backwards through an account's entries: WHERE account_id = @id AND id < @cursor ORDER BY id DESC.
        builder.HasIndex(e => new { e.AccountId, e.Id })
            .IsDescending(false, true);
    }
}
