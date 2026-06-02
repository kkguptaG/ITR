using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetsLiabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assets_liabilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_deposits = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    shares_and_securities = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    insurance_policies = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    loans_and_advances_given = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    cash_in_hand = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    jewellery_bullion = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    art_collections = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    vehicles = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    liabilities = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets_liabilities", x => x.id);
                    table.ForeignKey(
                        name: "fk_assets_liabilities_tax_returns_tax_return_id",
                        column: x => x.tax_return_id,
                        principalTable: "tax_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assets_liabilities_tax_return_id",
                table: "assets_liabilities",
                column: "tax_return_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assets_liabilities");
        }
    }
}
