using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paperless.Infrastructure;
using Paperless.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPaperlessPersistence(builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddDocumentSummaries();
builder.Services.AddGenAiWorker(builder.Configuration);
using var host = builder.Build();
await using (var scope = host.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
}
if (!builder.Configuration.GetValue<bool>("GenAi:Enabled"))
    host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GenAi")
        .LogInformation("GenAI is disabled. Summary jobs remain queued; no document text is sent to an external provider.");
await host.RunAsync();
