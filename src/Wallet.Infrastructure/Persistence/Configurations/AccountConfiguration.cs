using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallet.Core.Accounts;

namespace Wallet.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", t =>
        {
            t.HasCheckConstraint("ck_accounts_balance_non_negative", "allow_negative OR balance >= 0");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Type).HasConversion<short>();
        builder.Property(a => a.Status).HasConversion<short>();
        builder.Property(a => a.SystemCode).HasMaxLength(40);
        builder.Property(a => a.OwnerName).HasMaxLength(Account.OwnerNameMaxLength);

        // Npgsql maps a uint row version to the xmin system column.
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(a => a.SystemCode)
            .IsUnique()
            .HasFilter("system_code IS NOT NULL");

        builder.HasData(SystemAccounts.All);
    }
}
