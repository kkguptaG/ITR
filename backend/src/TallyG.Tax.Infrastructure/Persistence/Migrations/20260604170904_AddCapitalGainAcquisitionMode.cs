using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapitalGainAcquisitionMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "acquisition_mode",
                table: "capital_gains",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_rural_agricultural_land",
                table: "capital_gains",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "previous_owner_acquisition_date",
                table: "capital_gains",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "previous_owner_cost",
                table: "capital_gains",
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
                name: "acquisition_mode",
                table: "capital_gains");

            migrationBuilder.DropColumn(
                name: "is_rural_agricultural_land",
                table: "capital_gains");

            migrationBuilder.DropColumn(
                name: "previous_owner_acquisition_date",
                table: "capital_gains");

            migrationBuilder.DropColumn(
                name: "previous_owner_cost",
                table: "capital_gains");
        }
    }
}
