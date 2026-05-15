using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActorName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ActorRole = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Action_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "Action", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorName",
                table: "AuditEvents",
                column: "ActorName");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityType_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "EntityType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt",
                table: "AuditEvents",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");
        }
    }
}
