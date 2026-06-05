using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForm10Iea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "form10iea_ack_number",
                table: "tax_returns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "form10iea_date",
                table: "tax_returns",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "form10iea_ack_number",
                table: "tax_returns");

            migrationBuilder.DropColumn(
                name: "form10iea_date",
                table: "tax_returns");
        }
    }
}
