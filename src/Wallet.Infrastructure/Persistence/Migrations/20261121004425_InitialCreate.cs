using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Wallet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    system_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    owner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    balance = table.Column<long>(type: "bigint", nullable: false),
                    allow_negative = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                    table.CheckConstraint("ck_accounts_balance_non_negative", "allow_negative OR balance >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ledger_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    reverses_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_ledger_transactions_ledger_transactions_reverses_transactio",
                        column: x => x.reverses_transaction_id,
                        principalTable: "ledger_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    balance_after = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_entries", x => x.id);
                    table.CheckConstraint("ck_ledger_entries_amount_non_zero", "amount <> 0");
                    table.ForeignKey(
                        name: "fk_ledger_entries_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ledger_entries_ledger_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "ledger_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "accounts",
                columns: new[] { "id", "allow_negative", "balance", "created_at", "currency", "owner_name", "status", "system_code", "type" },
                values: new object[,]
                {
                    { new Guid("0196f1a0-0000-7000-8000-000000000001"), true, 0L, new DateTimeOffset(new DateTime(2026, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "TRY", "SETTLEMENT_TRY", (short)1, "SETTLEMENT_TRY", (short)2 },
                    { new Guid("0196f1a0-0000-7000-8000-000000000002"), true, 0L, new DateTimeOffset(new DateTime(2026, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "EUR", "SETTLEMENT_EUR", (short)1, "SETTLEMENT_EUR", (short)2 },
                    { new Guid("0196f1a0-0000-7000-8000-000000000003"), true, 0L, new DateTimeOffset(new DateTime(2026, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "SETTLEMENT_USD", (short)1, "SETTLEMENT_USD", (short)2 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_system_code",
                table: "accounts",
                column: "system_code",
                unique: true,
                filter: "system_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_account_id_id",
                table: "ledger_entries",
                columns: new[] { "account_id", "id" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_transaction_id",
                table: "ledger_entries",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_transactions_reverses_transaction_id",
                table: "ledger_transactions",
                column: "reverses_transaction_id",
                unique: true,
                filter: "reverses_transaction_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "ledger_transactions");
        }
    }
}
