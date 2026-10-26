using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Offers.Api.Features.OfferPublication.Migrations
{
    /// <inheritdoc />
    public partial class AddReportsAndInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfferInteractions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    OfferId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Action = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferInteractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferInteractions_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OfferReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    OfferId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Comment = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferReports_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OfferInteractions_OfferId",
                table: "OfferInteractions",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferReports_OfferId",
                table: "OfferReports",
                column: "OfferId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfferInteractions");

            migrationBuilder.DropTable(
                name: "OfferReports");
        }
    }
}
