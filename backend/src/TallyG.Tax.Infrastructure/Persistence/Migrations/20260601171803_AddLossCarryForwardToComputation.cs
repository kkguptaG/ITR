using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLossCarryForwardToComputation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "business_loss_carried_forward",
                table: "tax_computations",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "house_property_loss_carried_forward",
                table: "tax_computations",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "speculative_loss_carried_forward",
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
                name: "business_loss_carried_forward",
                table: "tax_computations");

            migrationBuilder.DropColumn(
                name: "house_property_loss_carried_forward",
                table: "tax_computations");

            migrationBuilder.DropColumn(
                name: "speculative_loss_carried_forward",
                table: "tax_computations");
        }
    }
}
