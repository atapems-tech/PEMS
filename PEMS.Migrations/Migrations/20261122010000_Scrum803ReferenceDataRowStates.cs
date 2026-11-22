using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PEMS.Persistence.Context;

#nullable disable

namespace PEMS.Persistence.Migrations
{
    /// <summary>SCRUM-803: the state of the rows the product ships into lookup tables (stable key, fingerprint of what it wrote). Written by hand, no designer file; the model snapshot carries the entity.</summary>
    [DbContext(typeof(PEMSDbContext))]
    [Migration("20261122010000_Scrum803ReferenceDataRowStates")]
    public partial class Scrum803ReferenceDataRowStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferenceDataRowStates",
                columns: table => new
                {
                    ReferenceDataRowStateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RowKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ShippedHash = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceDataRowStates", x => x.ReferenceDataRowStateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceDataRowStates_ListKey_RowKey",
                table: "ReferenceDataRowStates",
                columns: new[] { "ListKey", "RowKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferenceDataRowStates");
        }
    }
}
