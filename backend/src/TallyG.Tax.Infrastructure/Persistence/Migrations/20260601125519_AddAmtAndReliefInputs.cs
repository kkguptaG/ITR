using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAmtAndReliefInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "brought_forward_amt_credit",
                table: "tax_returns",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "foreign_dtaa_applies",
                table: "tax_returns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "foreign_income_doubly_taxed",
                table: "tax_returns",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "foreign_tax_paid",
                table: "tax_returns",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "relief89",
                table: "tax_returns",
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
                name: "brought_forward_amt_credit",
                table: "tax_returns");

            migrationBuilder.DropColumn(
                name: "foreign_dtaa_applies",
                table: "tax_returns");

            migrationBuilder.DropColumn(
                name: "foreign_income_doubly_taxed",
                table: "tax_returns");

            migrationBuilder.DropColumn(
                name: "foreign_tax_paid",
                table: "tax_returns");

            migrationBuilder.DropColumn(
                name: "relief89",
                table: "tax_returns");
        }
    }
}
