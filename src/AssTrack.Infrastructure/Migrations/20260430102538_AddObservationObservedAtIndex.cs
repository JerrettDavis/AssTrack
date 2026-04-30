using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddObservationObservedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Observations_ObservedAt",
                table: "Observations",
                column: "ObservedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Observations_ObservedAt",
                table: "Observations");
        }
    }
}
