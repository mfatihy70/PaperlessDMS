using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Paperless.Application.Abstractions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Repositories;

namespace Paperless.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<PaperlessDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IRepository<DocumentCategory>, Repository<DocumentCategory>>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
