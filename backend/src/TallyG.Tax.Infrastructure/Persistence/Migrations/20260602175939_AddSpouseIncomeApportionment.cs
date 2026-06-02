using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpouseIncomeApportionment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "spouse_income_apportionments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    spouse_name = table.Column<string>(type: "text", nullable: false),
                    spouse_pan = table.Column<string>(type: "text", nullable: false),
                    spouse_aadhaar = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spouse_income_apportionments", x => x.id);
                    table.ForeignKey(
                        name: "fk_spouse_income_apportionments_tax_returns_tax_return_id",
                        column: x => x.tax_return_id,
                        principalTable: "tax_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_spouse_income_apportionments_tax_return_id",
                table: "spouse_income_apportionments",
                column: "tax_return_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "spouse_income_apportionments");
        }
    }
}
