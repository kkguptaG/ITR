using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExemptIncomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exempt_incomes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    district = table.Column<string>(type: "text", nullable: true),
                    pin_code = table.Column<string>(type: "text", nullable: true),
                    land_measurement = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    land_owned = table.Column<bool>(type: "boolean", nullable: true),
                    land_irrigated = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exempt_incomes", x => x.id);
                    table.ForeignKey(
                        name: "fk_exempt_incomes_tax_returns_tax_return_id",
                        column: x => x.tax_return_id,
                        principalTable: "tax_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exempt_incomes_tax_return_id",
                table: "exempt_incomes",
                column: "tax_return_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exempt_incomes");
        }
    }
}
