using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Infrastructure.Data;

internal static class DatabaseSchemaUpgrade
{
    public static async Task EnsureUpToDateAsync(HelpdeskDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PlatformSettings" (
                "Key" TEXT NOT NULL CONSTRAINT "PK_PlatformSettings" PRIMARY KEY,
                "Value" TEXT NOT NULL,
                "UpdatedUtc" TEXT NOT NULL
            );
            """,
            cancellationToken);

        if (!await ColumnExistsAsync(dbContext, "Tickets", "ResolverGroupId", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Tickets\" ADD COLUMN \"ResolverGroupId\" TEXT NULL;",
                cancellationToken);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "ResolverGroups" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ResolverGroups" PRIMARY KEY,
                "CustomerId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "UpdatedUtc" TEXT NOT NULL,
                CONSTRAINT "FK_ResolverGroups_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ResolverGroups_CustomerId_Name\" ON \"ResolverGroups\" (\"CustomerId\", \"Name\");",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_ResolverGroups_CustomerId_IsActive_Name\" ON \"ResolverGroups\" (\"CustomerId\", \"IsActive\", \"Name\");",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "TicketCategories" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_TicketCategories" PRIMARY KEY,
                "CustomerId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "ResolverGroupId" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "UpdatedUtc" TEXT NOT NULL,
                CONSTRAINT "FK_TicketCategories_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_TicketCategories_ResolverGroups_ResolverGroupId" FOREIGN KEY ("ResolverGroupId") REFERENCES "ResolverGroups" ("Id") ON DELETE SET NULL
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TicketCategories_CustomerId_Name\" ON \"TicketCategories\" (\"CustomerId\", \"Name\");",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_TicketCategories_CustomerId_IsActive_Name\" ON \"TicketCategories\" (\"CustomerId\", \"IsActive\", \"Name\");",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Tickets_ResolverGroupId\" ON \"Tickets\" (\"ResolverGroupId\");",
            cancellationToken);
    }

    private static async Task<bool> ColumnExistsAsync(
        HelpdeskDbContext dbContext,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await command.Connection!.OpenAsync(cancellationToken);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string existingColumn = reader.GetString(1);
            if (existingColumn.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
