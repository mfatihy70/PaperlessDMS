namespace Paperless.Infrastructure.Persistence;

/// <summary>Applies the versioned database schema before the API accepts requests.</summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
