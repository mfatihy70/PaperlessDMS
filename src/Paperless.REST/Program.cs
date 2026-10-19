using Microsoft.EntityFrameworkCore;
using Paperless.REST.Data;
using Paperless.REST.Models;
using Paperless.REST.Repositories;
using Paperless.REST.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers
builder.Services.AddControllers();

// 2. Add Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Paperless REST API v1");
    c.RoutePrefix = "swagger";
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