using AgilineeringApi.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Infrastructure;

public class DatabaseMigrator(AppDbContext db, ILogger<DatabaseMigrator> logger)
{
    public void Apply()
    {
        // Add columns to existing DBs that predate these fields.
        // SQLite throws "duplicate column name" if already present — expected and ignored.
        TryAlterTable("ALTER TABLE Users ADD COLUMN FailedLoginAttempts INTEGER NOT NULL DEFAULT 0");
        TryAlterTable("ALTER TABLE Users ADD COLUMN LockoutEnd TEXT NULL");
        TryAlterTable("ALTER TABLE PostPreviews DROP COLUMN Name");

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS PostPreviews (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                PostId INTEGER NOT NULL REFERENCES Posts(Id) ON DELETE CASCADE,
                Token TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            )
            """);
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_PostPreviews_Token ON PostPreviews(Token)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_PostPreviews_PostId ON PostPreviews(PostId)");

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS PreviewComments (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                PreviewId INTEGER NOT NULL REFERENCES PostPreviews(Id) ON DELETE CASCADE,
                Body TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            )
            """);
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_PreviewComments_PreviewId ON PreviewComments(PreviewId)");

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Images (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Filename TEXT NOT NULL,
                ContentType TEXT NOT NULL,
                Data BLOB NOT NULL,
                Size INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL
            )
            """);
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Images_Filename ON Images(Filename)");

        // WAL mode — allows concurrent reads during writes (persistent, no-op on :memory:)
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL");

        // Indexes for common query patterns (idempotent — IF NOT EXISTS)
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Posts_AuthorId ON Posts(AuthorId)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Posts_Published_CreatedAt ON Posts(Published, CreatedAt DESC)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_PostTag_TagsId ON PostTag(TagsId)");
    }

    private void TryAlterTable(string sql)
    {
        try
        {
            db.Database.ExecuteSqlRaw(sql);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 &&
            (ex.Message.Contains("duplicate column name") || ex.Message.Contains("no such column")))
        {
            // Already in target state — expected when re-running migrations on an up-to-date database
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unexpected error running schema change: {Sql}", sql);
        }
    }

}
