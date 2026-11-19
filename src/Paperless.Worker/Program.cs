using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paperless.Infrastructure;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Processing;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("DefaultConnection"), builder.Configuration, runFileCleanup: false);
builder.Services.AddDocumentProcessing(builder.Configuration, dispatchOutbox: false);
builder.Services.AddHostedService<PreparationWorker>();
using var host = builder.Build();
await using (var scope = host.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
}
await host.RunAsync();
