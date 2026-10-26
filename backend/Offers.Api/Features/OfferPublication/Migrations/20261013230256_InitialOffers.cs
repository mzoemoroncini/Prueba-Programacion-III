using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Offers.Api.Features.OfferPublication.Migrations
{
    /// <inheritdoc />
    public partial class InitialOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Chain = table.Column<int>(type: "int", nullable: false),
                    SourceUrl = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false),
                    SourceOfferId = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true),
                    OriginalDescription = table.Column<string>(type: "longtext", nullable: true),
                    Summary = table.Column<string>(type: "longtext", nullable: true),
                    OriginalConditions = table.Column<string>(type: "longtext", nullable: true),
                    Conditions = table.Column<string>(type: "longtext", nullable: true),
                    Code = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    CodeStatus = table.Column<int>(type: "int", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastExtractedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offers", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OfferBenefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    OfferId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    OriginalDescription = table.Column<string>(type: "longtext", nullable: true),
                    Description = table.Column<string>(type: "longtext", nullable: true),
                    Requirements = table.Column<string>(type: "longtext", nullable: true),
                    Percentage = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PromotionalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: true),
                    PurchaseQuantity = table.Column<int>(type: "int", nullable: true),
                    ReceivedQuantity = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferBenefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferBenefits_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OfferBenefits_OfferId",
                table: "OfferBenefits",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_Status_StartsOn_EndsOn",
                table: "Offers",
                columns: new[] { "Status", "StartsOn", "EndsOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfferBenefits");

            migrationBuilder.DropTable(
                name: "Offers");
        }
    }
}
