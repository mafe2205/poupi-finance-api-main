using AppFinancesClassLibrary.Application.Categories.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface ICategoriesRepository
    {
        Task<Category> GetByIdAsync(long id);
        Task<Category> GetByIdIncludingInactiveAsync(long id);
        Task<IEnumerable<Category>> GetAllByUserIdAsync(long userId);
        Task<Category> CreateAsync(CreateCategoryDto dto);
        Task<Category> UpdateAsync(UpdateCategoryDto dto);
        Task<Category> DeleteAsync(long id);
        Task<IEnumerable<Category>> GetAllInactiveByUserIdAsync(long userId);
        Task<Category?> ReactivateAsync(long id);
    }
}
