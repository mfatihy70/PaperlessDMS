using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paperless.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOcrText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentTexts",
                schema: "public",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Text = table.Column<string>(type: "character varying(1000000)", maxLength: 1000000, nullable: false),
                    Language = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTexts", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_DocumentTexts_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "public",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTexts_JobId",
                schema: "public",
                table: "DocumentTexts",
                column: "JobId",
                unique: true);

            // AP3 stopped at ReadyForOcr. Give these originals a fresh generation and durable OCR delivery.
            // Stop old workers before upgrading so they cannot write the preparation-only state afterwards.
            migrationBuilder.Sql("""
                UPDATE public."ProcessingJobs"
                SET "JobId" = gen_random_uuid(), "CurrentMessageId" = gen_random_uuid(),
                    "State" = 'Queued', "Attempts" = 0, "CreatedAt" = now(), "UpdatedAt" = now(),
                    "StartedAt" = NULL, "FinishedAt" = NULL, "NextAttemptAt" = NULL,
                    "LeaseId" = NULL, "LeaseUntil" = NULL, "LastErrorCode" = NULL, "LastError" = NULL
                WHERE "State" = 'ReadyForOcr';

                INSERT INTO public."ProcessingOutbox"
                    ("Id", "DocumentId", "JobId", "IsDeadLetter", "Payload", "CreatedAt", "AvailableAt", "PublishAttempts")
                SELECT j."CurrentMessageId", j."DocumentId", j."JobId", false,
                       json_build_object('schemaVersion', 1, 'messageId', j."CurrentMessageId", 'jobId', j."JobId",
                                         'documentId', j."DocumentId", 'storageKey', j."StorageKey")::text,
                       now(), now(), 0
                FROM public."ProcessingJobs" j
                WHERE j."State" = 'Queued'
                  AND NOT EXISTS (SELECT 1 FROM public."ProcessingOutbox" m WHERE m."Id" = j."CurrentMessageId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM public."DocumentTexts")
                     OR EXISTS (SELECT 1 FROM public."ProcessingJobs" WHERE "State" = 'RecognizingText') THEN
                    RAISE EXCEPTION 'OCR results or running recognition exist. Back up and resolve them before rollback.';
                  END IF;
                END $$;
                UPDATE public."ProcessingJobs" SET "State" = 'ReadyForOcr' WHERE "State" = 'OcrCompleted';
                """);
            migrationBuilder.DropTable(
                name: "DocumentTexts",
                schema: "public");
        }
    }
}
