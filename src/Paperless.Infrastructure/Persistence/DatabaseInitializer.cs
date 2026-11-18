using Microsoft.EntityFrameworkCore;

namespace Paperless.Infrastructure.Persistence;

/// <summary>Preserves the existing database bootstrap and legacy upload upgrade during the architecture refactor.</summary>
public sealed class DatabaseInitializer(PaperlessDbContext database)
{
    public void Initialize()
    {
        if (database.Database.EnsureCreated())
            return;

        database.Database.ExecuteSqlRaw(
            """
            ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "FileContent" bytea NULL;
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Documents' AND column_name = 'Content'
                ) THEN
                    EXECUTE 'UPDATE "Documents" SET "FileContent" = convert_to("Content", ''UTF8'') WHERE "FileContent" IS NULL AND "Content" IS NOT NULL';
                END IF;
            END $$;
            ALTER TABLE "Documents" DROP COLUMN IF EXISTS "Content";
            """);
    }
}
