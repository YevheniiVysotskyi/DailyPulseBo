using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoroClubDailyPulseAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddIncludedEventsToReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IncludedEvents",
                table: "DailyReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludedEvents",
                table: "DailyReports");
        }
    }
}
