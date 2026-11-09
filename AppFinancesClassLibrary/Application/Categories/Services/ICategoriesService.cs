using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Categories.Entities.Dtos;
using AppFinancesClassLibrary.Application.Categories.Entities.Results;

namespace AppFinancesClassLibrary.Application.Categories.Services
{
    public interface ICategoriesService
    {
        Task<CategoryResult> CreateAsync(CreateCategoryDto dto);
        Task<CategoryResult> GetByIdAsync(long id);
        Task<IEnumerable<CategoryResult>> GetAllByUserIdAsync(long userId);
        Task<CategoryResult> UpdateAsync(long id, UpdateCategoryDto dto);
        Task<CategoryResult> DeleteAsync(long id);
        Task<IEnumerable<CategoryResult>> GetAllInactiveByUserIdAsync(long userId);
        Task<CategoryResult> ReactivateAsync(long id);
        Task<CategoryClassificationResult> SuggestAsync(string description);
    }
}
