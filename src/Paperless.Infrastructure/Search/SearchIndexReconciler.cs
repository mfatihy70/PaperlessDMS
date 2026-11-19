using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;

namespace Paperless.Infrastructure.Search;

public sealed class SearchIndexReconciler(IServiceScopeFactory scopes, IOptions<SearchOptions> options, ILogger<SearchIndexReconciler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var identity = await scope.ServiceProvider.GetRequiredService<ISearchIndex>().GetIdentityAsync(stoppingToken);
                var repository = scope.ServiceProvider.GetRequiredService<ISearchRepository>();
                await repository.ReindexAsync(identity, stoppingToken);
                await repository.RecoverUnavailableAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning(exception, "Search-index recovery check could not complete; it will be retried."); }
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.ReconcileIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
