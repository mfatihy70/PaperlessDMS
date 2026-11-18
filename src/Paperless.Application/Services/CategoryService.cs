using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.Application.Exceptions;
using Paperless.Application.Mapping;
using Paperless.Domain.Entities;

namespace Paperless.Application.Services;

public sealed class CategoryService(IRepository<DocumentCategory> repository) : ICategoryService
{
    public async Task<IEnumerable<CategoryResponseDto>> GetAllAsync()
    {
        var categories = await repository.GetAllAsync();
        return categories.Select(ResponseMapper.ToResponse);
    }

    public async Task<CategoryResponseDto?> GetByIdAsync(int id)
    {
        ValidateId(id);
        var category = await repository.GetByIdAsync(id);
        return category is null ? null : ResponseMapper.ToResponse(category);
    }

    public async Task<CategoryResponseDto> CreateAsync(string name, string? description)
    {
        ValidateName(name);
        var category = new DocumentCategory { Name = name.Trim(), Description = description };
        await repository.AddAsync(category);
        await repository.SaveChangesAsync();
        return ResponseMapper.ToResponse(category);
    }

    public async Task<bool> UpdateAsync(int id, string name, string? description)
    {
        ValidateId(id);
        ValidateName(name);
        var category = await repository.GetByIdAsync(id);
        if (category is null)
            return false;

        category.Name = name.Trim();
        category.Description = description;
        repository.Update(category);
        await repository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        ValidateId(id);
        var category = await repository.GetByIdAsync(id);
        if (category is null)
            return false;

        repository.Delete(category);
        await repository.SaveChangesAsync();
        return true;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new ApplicationValidationException("name", "A category name must contain 1 to 100 characters and cannot be blank.");
    }

    private static void ValidateId(int id)
    {
        if (id <= 0)
            throw new ApplicationValidationException("id", "The category ID must be a positive integer.");
    }
}
