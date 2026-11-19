using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Storage;

namespace Paperless.REST.Tests;

public class SwaggerTests(SwaggerTests.ApiFactory factory) : IClassFixture<SwaggerTests.ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OpenApi_ContainsEveryControllerEndpointWithDescriptions()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/swagger/v1/swagger.json"));
        var root = document.RootElement;
        Assert.Equal("Paperless REST API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("v1", root.GetProperty("info").GetProperty("version").GetString());

        string[] methods = ["get", "post", "put", "delete", "patch", "head", "options", "trace"];
        var operations = root.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(operation => methods.Contains(operation.Name))
                .Select(operation => (Key: $"{operation.Name} {path.Name}", Value: operation.Value)))
            .ToList();
        string[] expected =
        [
            "get /api/Documents", "get /api/Documents/{id}", "post /api/Documents",
            "get /api/Documents/{id}/file",
            "put /api/Documents/{id}", "delete /api/Documents/{id}",
            "get /api/Categories", "post /api/Categories",
            "get /api/Categories/{id}", "put /api/Categories/{id}", "delete /api/Categories/{id}"
        ];
        Assert.Equal(expected.Order(), operations.Select(operation => operation.Key).Order());
        Assert.All(operations, operation =>
            Assert.False(string.IsNullOrWhiteSpace(operation.Value.GetProperty("summary").GetString())));
        Assert.All(root.GetProperty("tags").EnumerateArray(), tag =>
            Assert.False(string.IsNullOrWhiteSpace(tag.GetProperty("description").GetString())));
    }

    [Theory]
    [InlineData("/api/Documents", "get", "200")]
    [InlineData("/api/Documents/{id}", "get", "200,400,404")]
    [InlineData("/api/Documents", "post", "201,400,409,413,502,503")]
    [InlineData("/api/Documents/{id}/file", "get", "200,400,404,502,503")]
    [InlineData("/api/Documents/{id}", "put", "204,400,404,409,413,502,503")]
    [InlineData("/api/Documents/{id}", "delete", "204,400,404,409")]
    [InlineData("/api/Categories", "get", "200")]
    [InlineData("/api/Categories", "post", "201,400")]
    public async Task OpenApi_DescribesActualResponseCodes(string path, string method, string expectedCodes)
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/swagger/v1/swagger.json"));
        var responses = document.RootElement.GetProperty("paths").GetProperty(path)
            .GetProperty(method).GetProperty("responses");
        Assert.Equal(expectedCodes.Split(',').Order(), responses.EnumerateObject().Select(item => item.Name).Order());
        if (responses.TryGetProperty("204", out var noContent))
            Assert.False(noContent.TryGetProperty("content", out _));
    }

    [Theory]
    [InlineData("DocumentResponseDto")]
    [InlineData("CreateCategoryDto")]
    [InlineData("CategoryResponseDto")]
    public async Task OpenApi_DescribesEveryDtoField(string schemaName)
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/swagger/v1/swagger.json"));
        var schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
        Assert.False(string.IsNullOrWhiteSpace(schema.GetProperty("description").GetString()));
        Assert.All(schema.GetProperty("properties").EnumerateObject(), property =>
            Assert.False(string.IsNullOrWhiteSpace(property.Value.GetProperty("description").GetString())));
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/")]
    [InlineData("/swagger/index.html")]
    public async Task SwaggerUi_IsReachableAndUsesRelativeDocumentUrl(string path)
    {
        using var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Paperless API Documentation", html);
        Assert.Contains("index.js", html);
        var initializer = await _client.GetStringAsync("/swagger/index.js");
        Assert.Contains("\"url\":\"v1/swagger.json\"", initializer);
    }

    [Theory]
    [InlineData("swagger-ui.css", "text/css")]
    [InlineData("swagger-ui-bundle.js", "text/javascript")]
    public async Task SwaggerUi_ServesItsAssets(string asset, string contentType)
    {
        using var response = await _client.GetAsync($"/swagger/{asset}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/Categories")]
    public async Task MalformedRequest_ReturnsDocumentedValidationProblem(string path)
    {
        using var content = new StringContent("{", Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(path, content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    public class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<MinioStorageOptions>(options => options.CleanupEnabled = false);
                // Only bypass database initialization. Exercise the real Program, controllers and Swagger middleware.
                var initializer = new Mock<IDatabaseInitializer>();
                initializer.Setup(db => db.InitializeAsync(It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
                services.RemoveAll<IDatabaseInitializer>();
                services.AddScoped(_ => initializer.Object);
            });
        }
    }
}
