using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignImmovableAndFinancialInterest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "foreign_financial_interests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    country_name = table.Column<string>(type: "text", nullable: false),
                    zip_code = table.Column<string>(type: "text", nullable: false),
                    nature_of_entity = table.Column<string>(type: "text", nullable: false),
                    entity_name = table.Column<string>(type: "text", nullable: false),
                    entity_address = table.Column<string>(type: "text", nullable: false),
                    nature_of_interest = table.Column<string>(type: "text", nullable: false),
                    date_held = table.Column<DateOnly>(type: "date", nullable: true),
                    total_investment = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    income_from_interest = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    nature_of_income = table.Column<string>(type: "text", nullable: false),
                    taxable_income_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    income_tax_schedule = table.Column<string>(type: "text", nullable: false),
                    income_tax_schedule_item = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_foreign_financial_interests", x => x.id);
                    table.ForeignKey(
                        name: "fk_foreign_financial_interests_tax_returns_tax_return_id",
                        column: x => x.tax_return_id,
                        principalTable: "tax_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "foreign_immovable_properties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    country_name = table.Column<string>(type: "text", nullable: false),
                    zip_code = table.Column<string>(type: "text", nullable: false),
                    address_of_property = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("pk_foreign_immovable_properties", x => x.id);
                    table.ForeignKey(
                        name: "fk_foreign_immovable_properties_tax_returns_tax_return_id",
                        column: x => x.tax_return_id,
                        principalTable: "tax_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_foreign_financial_interests_tax_return_id",
                table: "foreign_financial_interests",
                column: "tax_return_id");

            migrationBuilder.CreateIndex(
                name: "ix_foreign_immovable_properties_tax_return_id",
                table: "foreign_immovable_properties",
                column: "tax_return_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "foreign_financial_interests");

            migrationBuilder.DropTable(
                name: "foreign_immovable_properties");
        }
    }
}
