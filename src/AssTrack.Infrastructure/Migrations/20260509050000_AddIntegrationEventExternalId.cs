using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AssTrackDbContext))]
    [Migration("20260509050000_AddIntegrationEventExternalId")]
    public partial class AddIntegrationEventExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalEventId",
                table: "IntegrationEvents",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_Source_ExternalEventId",
                table: "IntegrationEvents",
                columns: new[] { "Source", "ExternalEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationEvents_Source_ExternalEventId",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "ExternalEventId",
                table: "IntegrationEvents");
        }
    }
}
