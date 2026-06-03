using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapitalGainBuyer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "capital_gain_buyers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capital_gain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_name = table.Column<string>(type: "text", nullable: false),
                    buyer_pan = table.Column<string>(type: "text", nullable: true),
                    buyer_aadhaar = table.Column<string>(type: "text", nullable: true),
                    percentage_share = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    address_of_property = table.Column<string>(type: "text", nullable: false),
                    state_code = table.Column<string>(type: "text", nullable: false),
                    pin_code = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capital_gain_buyers", x => x.id);
                    table.ForeignKey(
                        name: "fk_capital_gain_buyers_capital_gains_capital_gain_id",
                        column: x => x.capital_gain_id,
                        principalTable: "capital_gains",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_capital_gain_buyers_capital_gain_id",
                table: "capital_gain_buyers",
                column: "capital_gain_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capital_gain_buyers");
        }
    }
}
