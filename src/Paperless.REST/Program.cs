using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.OpenApi;
using Paperless.Application.Abstractions;
using Paperless.Application.Services;
using Paperless.Infrastructure;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Storage;
using Paperless.REST.ExceptionHandling;

var fileCommand = args.Length > 0 && args[0] == "files";
if (fileCommand && (args.Length != 2 || args[1] is not ("status" or "migrate" or "verify" or "purge" or "cleanup")))
{
    Console.Error.WriteLine("Usage: Paperless.REST files status|migrate|verify|purge|cleanup");
    Environment.ExitCode = 2;
    return;
}
var builder = WebApplication.CreateBuilder(fileCommand ? [] : args);

// 1. Add Controllers
builder.Services.AddControllers(options =>
    options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationValidationExceptionHandler>();
builder.Services.AddExceptionHandler<FileStorageExceptionHandler>();

// 2. Add Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Paperless REST API",
        Version = "v1",
        Description = "Manage uploaded documents, categories and asynchronous OCR for PDF, PNG and JPEG originals. " +
                      "Read processing status and persisted recognized text. AI summaries and full-text search are not implemented yet."
    });
    options.IncludeXmlComments(
        Path.Combine(AppContext.BaseDirectory, "Paperless.REST.xml"),
        includeControllerXmlComments: true);
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Paperless.Application.xml"));
});

// 3. Database Context
var connString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddInfrastructure(connString, builder.Configuration);
builder.Services.AddDocumentProcessing(builder.Configuration, dispatchOutbox: true);

// 4. DI Registrations
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException requestException
        ? requestException.StatusCode
        : StatusCodes.Status500InternalServerError
});
app.UseStatusCodePages();

// 5. Enable Swagger unconditionally (so it works inside Docker)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("v1/swagger.json", "Paperless REST API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Paperless API Documentation";
    c.EnableFilter();
    c.DisplayRequestDuration();
});

// IMPORTANT: DO NOT call app.UseHttpsRedirection() here!
// Containers run plain HTTP on internal networks.

// 6. Apply versioned migrations before accepting requests.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
}

if (fileCommand)
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    await using (var scope = app.Services.CreateAsyncScope())
    {
        Environment.ExitCode = await scope.ServiceProvider.GetRequiredService<FileMigrationService>()
            .RunAsync(args[1], cancellation.Token);
    }
    await app.DisposeAsync();
    return;
}

app.UseAuthorization();

// 7. Route mapping
app.MapControllers();

app.Run();

// Expose the entry point to integration tests.
public partial class Program { }
