using Paperless.Application.DTOs;

namespace Paperless.Application.Abstractions;

public interface ICategoryService
{
    Task<IEnumerable<CategoryResponseDto>> GetAllAsync();
    Task<CategoryResponseDto?> GetByIdAsync(int id);
    Task<CategoryResponseDto> CreateAsync(string name, string? description);
    Task<bool> UpdateAsync(int id, string name, string? description);
    Task<bool> DeleteAsync(int id);
}
