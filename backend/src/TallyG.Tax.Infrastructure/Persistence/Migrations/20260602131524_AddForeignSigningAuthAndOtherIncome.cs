using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignSigningAuthAndOtherIncome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "foreign_other_incomes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    country_name = table.Column<string>(type: "text", nullable: false),
                    zip_code = table.Column<string>(type: "text", nullable: false),
                    payer_name = table.Column<string>(type: "text", nullable: false),
                    payer_address = table.Column<string>(type: "text", nullable: false),
                    income_derived = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    nature_of_income = table.Column<string>(type: "text", nullable: false),
                    income_taxable = table.Column<bool>(type: "boolean", nullable: false),
                    income_offered = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    income_tax_schedule = table.Column<string>(type: "text", nullable: false),
                    income_tax_schedule_item = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_foreign_other_incomes", x => x.id);
                    table.ForeignKey(
                        name: "fk_foreign_other_incomes_tax_returns_tax_return_id",
                        column: x => x.tax_return_id,
                        principalTable: "tax_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "foreign_signing_authorities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    country_name = table.Column<string>(type: "text", nullable: false),
                    zip_code = table.Column<string>(type: "text", nullable: false),
                    institution_name = table.Column<string>(type: "text", nullable: false),
                    institution_address = table.Column<string>(type: "text", nullable: false),
                    account_holder_name = table.Column<string>(type: "text", nullable: false),
                    account_number = table.Column<string>(type: "text", nullable: false),
                    peak_balance_or_investment = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    income_taxable = table.Column<bool>(type: "boolean", nullable: false),
                    income_accrued = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    income_offered = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    income_tax_schedule = table.Column<string>(type: "text", nullable: false),
                    income_tax_schedule_item = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_foreign_signing_authorities", x => x.id);
                    table.ForeignKey(
                        name: "fk_foreign_signing_authorities_tax_returns_tax_return_id",
                        column: x => x.tax_return_id,
                        principalTable: "tax_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_foreign_other_incomes_tax_return_id",
                table: "foreign_other_incomes",
                column: "tax_return_id");

            migrationBuilder.CreateIndex(
                name: "ix_foreign_signing_authorities_tax_return_id",
                table: "foreign_signing_authorities",
                column: "tax_return_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "foreign_other_incomes");

            migrationBuilder.DropTable(
                name: "foreign_signing_authorities");
        }
    }
}
