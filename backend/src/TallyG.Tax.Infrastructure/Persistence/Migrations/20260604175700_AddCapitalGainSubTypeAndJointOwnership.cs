using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapitalGainSubTypeAndJointOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "co_owner_percent",
                table: "capital_gains",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "stt_paid",
                table: "capital_gains",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sub_type",
                table: "capital_gains",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tds_on_sale",
                table: "capital_gains",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "tds_section",
                table: "capital_gains",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "co_owner_percent",
                table: "capital_gains");

            migrationBuilder.DropColumn(
                name: "stt_paid",
                table: "capital_gains");

            migrationBuilder.DropColumn(
                name: "sub_type",
                table: "capital_gains");

            migrationBuilder.DropColumn(
                name: "tds_on_sale",
                table: "capital_gains");

            migrationBuilder.DropColumn(
                name: "tds_section",
                table: "capital_gains");
        }
    }
}
