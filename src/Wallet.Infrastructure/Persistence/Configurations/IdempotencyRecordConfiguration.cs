using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallet.Infrastructure.Idempotency;

namespace Wallet.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public const string PrimaryKeyName = "pk_idempotency_records";

    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");

        // A concurrent insert with the same key waits here until the first transaction ends.
        builder.HasKey(r => new { r.Key, r.Operation }).HasName(PrimaryKeyName);

        builder.Property(r => r.Key).HasMaxLength(IdempotencyKey.MaxKeyLength);
        builder.Property(r => r.Operation).HasMaxLength(80);
        builder.Property(r => r.RequestHash).HasColumnType("char(64)");
        builder.Property(r => r.ErrorCode).HasMaxLength(60);
        builder.Property(r => r.ErrorMessage).HasMaxLength(500);
        builder.Property(r => r.ErrorKind).HasConversion<short?>();

        builder.HasIndex(r => r.CreatedAt);
    }
}
