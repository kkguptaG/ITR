using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnFilingSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "filing_section",
                table: "tax_returns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "original_acknowledgment_number",
                table: "tax_returns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "original_filing_date",
                table: "tax_returns",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "filing_section",
                table: "tax_returns");

            migrationBuilder.DropColumn(
                name: "original_acknowledgment_number",
                table: "tax_returns");

            migrationBuilder.DropColumn(
                name: "original_filing_date",
                table: "tax_returns");
        }
    }
}
