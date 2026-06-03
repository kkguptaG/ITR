using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleIfPartnerFirmFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "firm_liable_to_audit",
                table: "firm_interests_al",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "profit_share_amount",
                table: "firm_interests_al",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "profit_share_percent",
                table: "firm_interests_al",
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
                name: "firm_liable_to_audit",
                table: "firm_interests_al");

            migrationBuilder.DropColumn(
                name: "profit_share_amount",
                table: "firm_interests_al");

            migrationBuilder.DropColumn(
                name: "profit_share_percent",
                table: "firm_interests_al");
        }
    }
}
