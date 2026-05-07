using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceServiceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceServiceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaintenanceScheduleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OdometerKm = table.Column<double>(type: "REAL", nullable: true),
                    RuntimeHours = table.Column<double>(type: "REAL", nullable: true),
                    PerformedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceServiceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceServiceRecords_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceServiceRecords_MaintenanceSchedules_MaintenanceScheduleId",
                        column: x => x.MaintenanceScheduleId,
                        principalTable: "MaintenanceSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceServiceRecords_AssetId_CompletedAt",
                table: "MaintenanceServiceRecords",
                columns: new[] { "AssetId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceServiceRecords_MaintenanceScheduleId_CompletedAt",
                table: "MaintenanceServiceRecords",
                columns: new[] { "MaintenanceScheduleId", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceServiceRecords");
        }
    }
}
