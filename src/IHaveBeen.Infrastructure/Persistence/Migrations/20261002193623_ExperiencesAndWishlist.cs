using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHaveBeen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExperiencesAndWishlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "VisitedOn",
                table: "TravelLogs",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedOn",
                table: "TravelLogs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "TravelLogs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Visited");

            migrationBuilder.CreateTable(
                name: "Experiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TravelLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    VisitedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Experiences", x => x.Id);
                    table.CheckConstraint("CK_Experience_Rating", "\"Rating\" BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_Experiences_TravelLogs_TravelLogId",
                        column: x => x.TravelLogId,
                        principalTable: "TravelLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Log_Status",
                table: "TravelLogs",
                sql: "\"Status\" IN ('Visited', 'Wishlist') AND (\"Status\" = 'Wishlist' OR (\"VisitedOn\" IS NOT NULL AND length(trim(\"City\")) > 0))");

            migrationBuilder.CreateIndex(
                name: "IX_Experiences_TravelLogId_VisitedOn",
                table: "Experiences",
                columns: new[] { "TravelLogId", "VisitedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A rollback must not fabricate visit dates for wishlist entries.
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"TravelLogs\" WHERE \"VisitedOn\" IS NULL) THEN RAISE EXCEPTION 'Convert or export wishlist entries before rolling back this migration.'; END IF; END $$;");
            migrationBuilder.DropTable(
                name: "Experiences");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Log_Status",
                table: "TravelLogs");

            migrationBuilder.DropColumn(
                name: "PlannedOn",
                table: "TravelLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "TravelLogs");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "VisitedOn",
                table: "TravelLogs",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
