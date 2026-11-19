using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paperless.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessingJobs",
                schema: "public",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingJobs", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_ProcessingJobs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "public",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingOutbox",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeadLetter = table.Column<bool>(type: "boolean", nullable: false),
                    Payload = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AvailableAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PublishLeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishLeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingOutbox_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "public",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingJobs_JobId",
                schema: "public",
                table: "ProcessingJobs",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingOutbox_DocumentId_JobId",
                schema: "public",
                table: "ProcessingOutbox",
                columns: new[] { "DocumentId", "JobId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingOutbox_PublishedAt_AvailableAt",
                schema: "public",
                table: "ProcessingOutbox",
                columns: new[] { "PublishedAt", "AvailableAt" });

            // Existing MinIO originals get the same durable preparation flow as new uploads.
            // Legacy database-only bytes remain untouched and require the explicit files migrate command.
            migrationBuilder.Sql("""
                INSERT INTO public."ProcessingJobs"
                    ("DocumentId", "JobId", "CurrentMessageId", "StorageKey", "FileSha256", "FileSizeBytes",
                     "State", "Attempts", "CreatedAt", "UpdatedAt")
                SELECT "Id", gen_random_uuid(), gen_random_uuid(), "StorageKey", "FileSha256", "FileSizeBytes",
                       'Queued', 0, now(), now()
                FROM public."Documents"
                WHERE "StorageKey" ~ '^documents/[0-9a-f]{32}$'
                  AND "FileSha256" ~ '^[0-9A-Fa-f]{64}$' AND "FileSizeBytes" BETWEEN 1 AND 26214400;

                INSERT INTO public."ProcessingOutbox"
                    ("Id", "DocumentId", "JobId", "IsDeadLetter", "Payload", "CreatedAt", "AvailableAt", "PublishAttempts")
                SELECT "CurrentMessageId", "DocumentId", "JobId", false,
                       json_build_object('schemaVersion', 1, 'messageId', "CurrentMessageId", 'jobId', "JobId",
                                         'documentId', "DocumentId", 'storageKey', "StorageKey")::text,
                       now(), now(), 0
                FROM public."ProcessingJobs";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM public."ProcessingJobs" WHERE "State" IN ('Queued', 'Processing', 'RetryScheduled'))
                     OR EXISTS (SELECT 1 FROM public."ProcessingOutbox" WHERE "PublishedAt" IS NULL) THEN
                    RAISE EXCEPTION 'Pending processing work exists. Resolve it and back up the database before rolling back.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "ProcessingJobs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ProcessingOutbox",
                schema: "public");
        }
    }
}
