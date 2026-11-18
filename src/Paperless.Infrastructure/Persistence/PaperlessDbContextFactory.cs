using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Paperless.Infrastructure.Persistence;

/// <summary>Lets EF tooling inspect the model without starting the HTTP application or migrating a database.</summary>
public sealed class PaperlessDbContextFactory : IDesignTimeDbContextFactory<PaperlessDbContext>
{
    public PaperlessDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection before running the EF Core tools.");

        var options = new DbContextOptionsBuilder<PaperlessDbContext>()
            .UseNpgsql(connectionString, postgres =>
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options;
        return new PaperlessDbContext(options);
    }
}
