using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignCashValueOtherAssetsTrusts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "foreign_cash_value_insurances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    country_name = table.Column<string>(type: "text", nullable: false),
                    institution_name = table.Column<string>(type: "text", nullable: false),
                    institution_address = table.Column<string>(type: "text", nullable: false),
                    zip_code = table.Column<string>(type: "text", nullable: false),
                    contract_date = table.Column<DateOnly>(type: "date", nullable: true),
                    cash_or_surrender_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    gross_amount_credited = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_foreign_cash_value_insurances", x => x.id);
                    table.ForeignKey(
                        name: "fk_foreign_cash_value_insurances_tax_returns_tax_return_id",
                        column: x => x.tax_return_id,
                        principalTable: "tax_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "foreign_other_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    country_name = table.Column<string>(type: "text", nullable: false),
                    zip_code = table.Column<string>(type: "text", nullable: false),
                    nature_of_asset = table.Column<string>(type: "text", nullable: false),
                    ownership = table.Column<string>(type: "text", nullable: false),
                    acquisition_date = table.Column<DateOnly>(type: "date", nullable: true),
                    total_investment = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    income_derived = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    nature_of_income = table.Column<string>(type: "text", nullable: false),
                    taxable_income_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    income_tax_schedule = table.Column<string>(type: "text", nullable: false),
                    income_tax_schedule_item = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_foreign_other_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_foreign_other_assets_tax_returns_tax_return_id",
                        column: x => x.tax_return_id,
                        principalTable: "tax_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "foreign_trust_interests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    country_name = table.Column<string>(type: "text", nullable: false),
                    zip_code = table.Column<string>(type: "text", nullable: false),
                    trust_name = table.Column<string>(type: "text", nullable: false),
                    trust_address = table.Column<string>(type: "text", nullable: false),
                    trustee_names = table.Column<string>(type: "text", nullable: false),
                    trustee_addresses = table.Column<string>(type: "text", nullable: false),
                    settlor_name = table.Column<string>(type: "text", nullable: false),
                    settlor_address = table.Column<string>(type: "text", nullable: false),
                    beneficiary_names = table.Column<string>(type: "text", nullable: false),
                    beneficiary_addresses = table.Column<string>(type: "text", nullable: false),
                    date_held = table.Column<DateOnly>(type: "date", nullable: true),
                    income_taxable = table.Column<bool>(type: "boolean", nullable: false),
                    income_from_trust = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    income_offered = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    income_tax_schedule = table.Column<string>(type: "text", nullable: false),
                    income_tax_schedule_item = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_foreign_trust_interests", x => x.id);
                    table.ForeignKey(
                        name: "fk_foreign_trust_interests_tax_returns_tax_return_id",
                        column: x => x.tax_return_id,
                        principalTable: "tax_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_foreign_cash_value_insurances_tax_return_id",
                table: "foreign_cash_value_insurances",
                column: "tax_return_id");

            migrationBuilder.CreateIndex(
                name: "ix_foreign_other_assets_tax_return_id",
                table: "foreign_other_assets",
                column: "tax_return_id");

            migrationBuilder.CreateIndex(
                name: "ix_foreign_trust_interests_tax_return_id",
                table: "foreign_trust_interests",
                column: "tax_return_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "foreign_cash_value_insurances");

            migrationBuilder.DropTable(
                name: "foreign_other_assets");

            migrationBuilder.DropTable(
                name: "foreign_trust_interests");
        }
    }
}
