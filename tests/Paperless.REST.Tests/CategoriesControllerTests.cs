using Microsoft.AspNetCore.Mvc;
using Moq;
using Paperless.REST.Controllers;
using Paperless.REST.DTOs;
using Paperless.REST.Models;
using Paperless.REST.Repositories;
using Xunit;

namespace Paperless.REST.Tests;

public class CategoriesControllerTests
{
    private readonly Mock<IRepository<DocumentCategory>> _repository = new();
    private readonly CategoriesController _controller;

    public CategoriesControllerTests()
    {
        _controller = new CategoriesController(_repository.Object);
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ReturnsEmptyOkResult()
    {
        _repository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<DocumentCategory>());
        var result = await _controller.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<CategoryResponseDto>>(ok.Value));
    }

    [Fact]
    public async Task GetAll_ReturnsMappedCategories()
    {
        _repository.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            new DocumentCategory { Id = 1, Name = "Finance", Description = "Invoices" },
            new DocumentCategory { Id = 2, Name = "Legal" }
        });

        var result = await _controller.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<CategoryResponseDto>>(ok.Value).ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Finance", items[0].Name);
        Assert.Equal("Invoices", items[0].Description);
        Assert.Null(items[1].Description);
        _repository.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task Create_SavesCategoryAndReturnsCreatedResult()
    {
        var input = new CreateCategoryDto("Finance", "Invoices");
        DocumentCategory? saved = null;
        _repository.Setup(x => x.AddAsync(It.IsAny<DocumentCategory>()))
            .Callback<DocumentCategory>(c => { saved = c; c.Id = 15; })
            .Returns(Task.CompletedTask);
        _repository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _controller.Create(input);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CategoriesController.GetAll), created.ActionName);
        Assert.Equal(15, created.RouteValues!["id"]);
        var response = Assert.IsType<CategoryResponseDto>(created.Value);
        Assert.Equal(15, response.Id);
        Assert.Equal("Finance", response.Name);
        Assert.Equal("Invoices", response.Description);
        Assert.Equal("Finance", saved!.Name);
        _repository.Verify(x => x.AddAsync(It.IsAny<DocumentCategory>()), Times.Once);
        _repository.Verify(x => x.SaveChangesAsync(), Times.Once);
    }
}
