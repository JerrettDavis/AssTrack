using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    SubjectType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    SubjectId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    SubjectName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_CorrelationId",
                table: "IntegrationEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_EventType_OccurredAt",
                table: "IntegrationEvents",
                columns: new[] { "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_OccurredAt",
                table: "IntegrationEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_Source_OccurredAt",
                table: "IntegrationEvents",
                columns: new[] { "Source", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_SubjectType_SubjectId",
                table: "IntegrationEvents",
                columns: new[] { "SubjectType", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationEvents");
        }
    }
}
