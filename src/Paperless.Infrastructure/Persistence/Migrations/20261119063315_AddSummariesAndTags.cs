using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Paperless.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSummariesAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SummaryJobs",
                schema: "public",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SummaryJobs", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_SummaryJobs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "public",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSummaries",
                schema: "public",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSummaries", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_DocumentSummaries_SummaryJobs_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "public",
                        principalTable: "SummaryJobs",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SummaryOutbox",
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
                    table.PrimaryKey("PK_SummaryOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SummaryOutbox_SummaryJobs_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "public",
                        principalTable: "SummaryJobs",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTags",
                schema: "public",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    TagId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTags", x => new { x.DocumentId, x.TagId });
                    table.ForeignKey(
                        name: "FK_DocumentTags_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "public",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentTags_Tags_TagId",
                        column: x => x.TagId,
                        principalSchema: "public",
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_TagId",
                schema: "public",
                table: "DocumentTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_SummaryJobs_JobId",
                schema: "public",
                table: "SummaryJobs",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SummaryOutbox_DocumentId",
                schema: "public",
                table: "SummaryOutbox",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SummaryOutbox_PublishedAt_AvailableAt",
                schema: "public",
                table: "SummaryOutbox",
                columns: new[] { "PublishedAt", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NormalizedName",
                schema: "public",
                table: "Tags",
                column: "NormalizedName",
                unique: true);

            // Queue existing current OCR results too. Enabling the worker later opts into sending this text.
            migrationBuilder.Sql("""
                INSERT INTO public."SummaryJobs"
                    ("DocumentId", "JobId", "SourceJobId", "CurrentMessageId", "State", "Attempts", "CreatedAt", "UpdatedAt")
                SELECT d."Id", gen_random_uuid(), t."JobId", gen_random_uuid(), 'Queued', 0, now(), now()
                FROM public."Documents" d
                JOIN public."DocumentTexts" t ON t."DocumentId" = d."Id"
                JOIN public."ProcessingJobs" p ON p."DocumentId" = d."Id"
                WHERE t."StorageKey" = d."StorageKey" AND t."JobId" = p."JobId" AND p."State" = 'OcrCompleted';

                INSERT INTO public."SummaryOutbox"
                    ("Id", "DocumentId", "JobId", "IsDeadLetter", "Payload", "CreatedAt", "AvailableAt", "PublishAttempts")
                SELECT "CurrentMessageId", "DocumentId", "JobId", false,
                       json_build_object('schemaVersion', 1, 'messageId', "CurrentMessageId", 'jobId', "JobId", 'documentId', "DocumentId")::text,
                       now(), now(), 0
                FROM public."SummaryJobs";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM public."SummaryJobs") OR EXISTS (SELECT 1 FROM public."Tags") THEN
                    RAISE EXCEPTION 'Summary or tag data exists. Back up and explicitly retire that data before rollback.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "DocumentSummaries",
                schema: "public");

            migrationBuilder.DropTable(
                name: "DocumentTags",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SummaryOutbox",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Tags",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SummaryJobs",
                schema: "public");
        }
    }
}
