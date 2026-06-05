using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyG.Tax.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapitalGainExemptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "exemptions_json",
                table: "capital_gains",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exemptions_json",
                table: "capital_gains");
        }
    }
}
