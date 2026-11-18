using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.OpenApi;
using Paperless.Application.Abstractions;
using Paperless.Application.Services;
using Paperless.Infrastructure;
using Paperless.Infrastructure.Persistence;
using Paperless.REST.ExceptionHandling;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers
builder.Services.AddControllers(options =>
    options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationValidationExceptionHandler>();

// 2. Add Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Paperless REST API",
        Version = "v1",
        Description = "Manage uploaded documents and categories. OCR, " +
                      "AI summaries and full-text search are not implemented yet."
    });
    options.IncludeXmlComments(
        Path.Combine(AppContext.BaseDirectory, "Paperless.REST.xml"),
        includeControllerXmlComments: true);
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Paperless.Application.xml"));
});

// 3. Database Context
var connString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddInfrastructure(connString);

// 4. DI Registrations
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

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

// 6. Ensure the database and upgrade existing document tables for original file storage.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().Initialize();
}

app.UseAuthorization();

// 7. Route mapping
app.MapControllers();

app.Run();

// Expose the entry point to integration tests.
public partial class Program { }
