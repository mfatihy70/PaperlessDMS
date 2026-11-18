using Microsoft.EntityFrameworkCore;

namespace Paperless.Infrastructure.Persistence;

/// <summary>Applies pending migrations, including the one-time adoption of known legacy schemas.</summary>
public sealed class DatabaseInitializer(PaperlessDbContext database) : IDatabaseInitializer
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        database.Database.MigrateAsync(cancellationToken);
}
