using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paperless.Infrastructure;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Search;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPaperlessPersistence(builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddDocumentSearch(builder.Configuration);
builder.Services.AddHostedService<SearchIndexWorker>();
builder.Services.AddHostedService<SearchIndexReconciler>();
using var host = builder.Build();
await using (var scope = host.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
}
await host.RunAsync();
