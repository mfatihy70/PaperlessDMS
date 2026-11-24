using Moq;
using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;
using Paperless.Application.Services;
using Paperless.Domain.Entities;
using Xunit;

namespace Paperless.REST.Tests;

public class CategoryServiceTests
{
    private readonly Mock<IRepository<DocumentCategory>> _repository = new();
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _service = new CategoryService(_repository.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task Create_InvalidName_Throws(string name)
    {
        await Assert.ThrowsAsync<ApplicationValidationException>(() => _service.CreateAsync(name, null));
        _repository.Verify(x => x.AddAsync(It.IsAny<DocumentCategory>()), Times.Never);
    }

    [Fact]
    public async Task Create_NameTooLong_Throws()
    {
        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            _service.CreateAsync(new string('A', 101), null));

        _repository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_TrimsNameAndSaves()
    {
        DocumentCategory? saved = null;
        _repository.Setup(x => x.AddAsync(It.IsAny<DocumentCategory>()))
            .Callback<DocumentCategory>(category =>
            {
                saved = category;
                category.Id = 10;
            })
            .Returns(Task.CompletedTask);
        _repository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.CreateAsync("  Invoices  ", "Bills");

        Assert.NotNull(saved);
        Assert.Equal("Invoices", saved.Name);
        Assert.Equal("Invoices", result.Name);
        Assert.Equal(10, result.Id);
        _repository.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_ExistingCategory_SavesChanges()
    {
        var category = new DocumentCategory { Id = 5, Name = "Old name" };
        _repository.Setup(x => x.GetByIdAsync(5)).ReturnsAsync(category);
        _repository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.UpdateAsync(5, "  New name  ", "Updated description");

        Assert.True(result);
        Assert.Equal("New name", category.Name);
        Assert.Equal("Updated description", category.Description);
        _repository.Verify(x => x.Update(category), Times.Once);
        _repository.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_UnknownCategory_ReturnsFalse()
    {
        _repository.Setup(x => x.GetByIdAsync(99)).ReturnsAsync((DocumentCategory?)null);

        var result = await _service.UpdateAsync(99, "New name", null);

        Assert.False(result);
        _repository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Delete_ExistingCategory_DeletesAndSaves()
    {
        var category = new DocumentCategory { Id = 5, Name = "Invoices" };
        _repository.Setup(x => x.GetByIdAsync(5)).ReturnsAsync(category);
        _repository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.DeleteAsync(5);

        Assert.True(result);
        _repository.Verify(x => x.Delete(category), Times.Once);
        _repository.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_UnknownCategory_ReturnsFalse()
    {
        _repository.Setup(x => x.GetByIdAsync(99)).ReturnsAsync((DocumentCategory?)null);

        var result = await _service.DeleteAsync(99);

        Assert.False(result);
        _repository.Verify(x => x.Delete(It.IsAny<DocumentCategory>()), Times.Never);
        _repository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Delete_InvalidId_Throws(int id)
    {
        await Assert.ThrowsAsync<ApplicationValidationException>(() => _service.DeleteAsync(id));
        _repository.Verify(x => x.Delete(It.IsAny<DocumentCategory>()), Times.Never);
    }
}
