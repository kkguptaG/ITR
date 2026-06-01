using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAmtReliefOutputsToComputation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "adjusted_total_income",
                table: "tax_computations",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "alternative_minimum_tax",
                table: "tax_computations",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "amt_credit_generated",
                table: "tax_computations",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "amt_credit_set_off",
                table: "tax_computations",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "relief89",
                table: "tax_computations",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "relief90and91",
                table: "tax_computations",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "adjusted_total_income",
                table: "tax_computations");

            migrationBuilder.DropColumn(
                name: "alternative_minimum_tax",
                table: "tax_computations");

            migrationBuilder.DropColumn(
                name: "amt_credit_generated",
                table: "tax_computations");

            migrationBuilder.DropColumn(
                name: "amt_credit_set_off",
                table: "tax_computations");

            migrationBuilder.DropColumn(
                name: "relief89",
                table: "tax_computations");

            migrationBuilder.DropColumn(
                name: "relief90and91",
                table: "tax_computations");
        }
    }
}
