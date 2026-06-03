using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessFinancialParticularsAnd44AE : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "bank_balance",
                table: "business_incomes",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cash_balance",
                table: "business_incomes",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "fixed_assets",
                table: "business_incomes",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "goods_carriage_json",
                table: "business_incomes",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<decimal>(
                name: "inventory",
                table: "business_incomes",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "partner_capital",
                table: "business_incomes",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "secured_loans",
                table: "business_incomes",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "sundry_creditors",
                table: "business_incomes",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "sundry_debtors",
                table: "business_incomes",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "unsecured_loans",
                table: "business_incomes",
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
                name: "bank_balance",
                table: "business_incomes");

            migrationBuilder.DropColumn(
                name: "cash_balance",
                table: "business_incomes");

            migrationBuilder.DropColumn(
                name: "fixed_assets",
                table: "business_incomes");

            migrationBuilder.DropColumn(
                name: "goods_carriage_json",
                table: "business_incomes");

            migrationBuilder.DropColumn(
                name: "inventory",
                table: "business_incomes");

            migrationBuilder.DropColumn(
                name: "partner_capital",
                table: "business_incomes");

            migrationBuilder.DropColumn(
                name: "secured_loans",
                table: "business_incomes");

            migrationBuilder.DropColumn(
                name: "sundry_creditors",
                table: "business_incomes");

            migrationBuilder.DropColumn(
                name: "sundry_debtors",
                table: "business_incomes");

            migrationBuilder.DropColumn(
                name: "unsecured_loans",
                table: "business_incomes");
        }
    }
}
