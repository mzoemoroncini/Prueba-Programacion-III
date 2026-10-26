using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Offers.Api.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class AddGeminiBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeminiBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    QuotaDay = table.Column<DateTime>(type: "date", nullable: false),
                    RequestCount = table.Column<int>(type: "int", nullable: false),
                    NextRequestAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BlockedUntilUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeminiBudgets", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeminiBudgets");
        }
    }
}
