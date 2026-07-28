using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrontiereLiveGe.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BorderPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BorderPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BotSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PostingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinMinutesBetweenPosts = table.Column<int>(type: "INTEGER", nullable: false),
                    RisingThresholdMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CriticalDelayMinutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BorderPointId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Trend = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPosted = table.Column<bool>(type: "INTEGER", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    PredictedDelayMinutes = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertEvents_BorderPoints_BorderPointId",
                        column: x => x.BorderPointId,
                        principalTable: "BorderPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrafficSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BorderPointId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EstimatedDelayMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeedKmh = table.Column<int>(type: "INTEGER", nullable: false),
                    CongestionLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrafficSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrafficSnapshots_BorderPoints_BorderPointId",
                        column: x => x.BorderPointId,
                        principalTable: "BorderPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_BorderPointId_CreatedAtUtc",
                table: "AlertEvents",
                columns: new[] { "BorderPointId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_Fingerprint",
                table: "AlertEvents",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_BorderPoints_Name",
                table: "BorderPoints",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BotSettings_Id",
                table: "BotSettings",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TrafficSnapshots_BorderPointId_RecordedAtUtc",
                table: "TrafficSnapshots",
                columns: new[] { "BorderPointId", "RecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertEvents");

            migrationBuilder.DropTable(
                name: "BotSettings");

            migrationBuilder.DropTable(
                name: "TrafficSnapshots");

            migrationBuilder.DropTable(
                name: "BorderPoints");
        }
    }
}
