using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedReturnFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "original_return_previously_filed",
                table: "tax_returns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "original_tax_paid",
                table: "tax_returns",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "updated_return_reason",
                table: "tax_returns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "updated_return_tier",
                table: "tax_returns",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "original_return_previously_filed",
                table: "tax_returns");

            migrationBuilder.DropColumn(
                name: "original_tax_paid",
                table: "tax_returns");

            migrationBuilder.DropColumn(
                name: "updated_return_reason",
                table: "tax_returns");

            migrationBuilder.DropColumn(
                name: "updated_return_tier",
                table: "tax_returns");
        }
    }
}
