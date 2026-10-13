using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Offers.Api.Features.OfferCapture.Migrations
{
    /// <inheritdoc />
    public partial class InitialCaptureRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CaptureRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    SourceId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    SourceUrl = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Stage = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    OfferCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureRuns", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureRuns_StartedAt",
                table: "CaptureRuns",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaptureRuns");
        }
    }
}
