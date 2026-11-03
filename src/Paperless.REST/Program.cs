using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Paperless.REST.Data;
using Paperless.REST.Models;
using Paperless.REST.Repositories;
using Paperless.REST.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers
builder.Services.AddControllers();

// 2. Add Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Paperless REST API",
        Version = "v1",
        Description = "Manage document metadata and categories. Document file upload, OCR, " +
                      "AI summaries and full-text search are not implemented yet."
    });
    options.IncludeXmlComments(
        Path.Combine(AppContext.BaseDirectory, "Paperless.REST.xml"),
        includeControllerXmlComments: true);
});

// 3. Database Context
var connString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<PaperlessDbContext>(options =>
    options.UseNpgsql(connString));

// 4. DI Registrations
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IRepository<DocumentCategory>, Repository<DocumentCategory>>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

var app = builder.Build();

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

// 6. DB Migration / EnsureCreated
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaperlessDbContext>();
    db.Database.EnsureCreated();
}

app.UseAuthorization();

// 7. Route mapping
app.MapControllers();

app.Run();

// Expose the entry point to integration tests.
public partial class Program { }
