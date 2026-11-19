using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paperless.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "SearchIndexRevisions",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "SearchIndexCatalog",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Identity = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchIndexCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchIndexJobs",
                schema: "public",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchIndexJobs", x => x.DocumentId);
                });

            migrationBuilder.CreateTable(
                name: "SearchOutbox",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_SearchOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SearchOutbox_SearchIndexJobs_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "public",
                        principalTable: "SearchIndexJobs",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchOutbox_DocumentId_Revision",
                schema: "public",
                table: "SearchOutbox",
                columns: new[] { "DocumentId", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchOutbox_PublishedAt_AvailableAt",
                schema: "public",
                table: "SearchOutbox",
                columns: new[] { "PublishedAt", "AvailableAt" });

            // The index is derived from current PostgreSQL data; originals/OCR text stay untouched.
            migrationBuilder.Sql("""
                INSERT INTO public."SearchIndexJobs"
                    ("DocumentId", "Revision", "MessageId", "SourceJobId", "Action", "State", "Attempts", "CreatedAt", "UpdatedAt")
                SELECT d."Id", nextval('public."SearchIndexRevisions"'), gen_random_uuid(),
                       CASE WHEN t."JobId" = p."JobId" AND t."StorageKey" = d."StorageKey" AND p."State" = 'OcrCompleted' THEN t."JobId" END,
                       CASE WHEN t."JobId" = p."JobId" AND t."StorageKey" = d."StorageKey" AND p."State" = 'OcrCompleted' THEN 'Index' ELSE 'Remove' END,
                       'Queued', 0, now(), now()
                FROM public."Documents" d
                LEFT JOIN public."DocumentTexts" t ON t."DocumentId" = d."Id"
                LEFT JOIN public."ProcessingJobs" p ON p."DocumentId" = d."Id";

                INSERT INTO public."SearchOutbox"
                    ("Id", "DocumentId", "Revision", "IsDeadLetter", "Payload", "CreatedAt", "AvailableAt", "PublishAttempts")
                SELECT "MessageId", "DocumentId", "Revision", false,
                       json_build_object('schemaVersion', 1, 'messageId', "MessageId", 'documentId', "DocumentId", 'revision', "Revision")::text,
                       now(), now(), 0
                FROM public."SearchIndexJobs";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM public."SearchIndexJobs") OR EXISTS (SELECT 1 FROM public."SearchOutbox") THEN
                    RAISE EXCEPTION 'Search revision history exists. Back up and retire the derived index before rollback.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "SearchIndexCatalog",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SearchOutbox",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SearchIndexJobs",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "SearchIndexRevisions",
                schema: "public");
        }
    }
}
