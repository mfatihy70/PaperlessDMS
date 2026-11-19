using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paperless.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddObjectStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileSha256",
                schema: "public",
                table: "Documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                schema: "public",
                table: "Documents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "public",
                table: "Documents",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "FileCleanupJobs",
                schema: "public",
                columns: table => new
                {
                    ObjectKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NotBefore = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileCleanupJobs", x => x.ObjectKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_StorageKey",
                schema: "public",
                table: "Documents",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileCleanupJobs_NotBefore",
                schema: "public",
                table: "FileCleanupJobs",
                column: "NotBefore");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."Documents" WHERE "StorageKey" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM public."FileCleanupJobs") THEN
                        RAISE EXCEPTION 'Cannot remove object storage metadata while files or cleanup jobs exist. Restore a coordinated database and MinIO backup instead.';
                    END IF;
                END
                $rollback$;
                """);
            migrationBuilder.DropTable(
                name: "FileCleanupJobs",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_Documents_StorageKey",
                schema: "public",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileSha256",
                schema: "public",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                schema: "public",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "public",
                table: "Documents");
        }
    }
}
