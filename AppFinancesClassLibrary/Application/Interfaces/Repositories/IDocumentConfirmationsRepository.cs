using AppFinancesClassLibrary.Application.Categories.Classification;
using AppFinancesClassLibrary.Application.Documents.Entities.Dtos;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IDocumentConfirmationsRepository
    {
        Task CreateAsync(CreateDocumentConfirmationDto dto);
        Task CreateItemsAsync(long documentId, IEnumerable<ConfirmDocumentItemDto> items);
        Task<IEnumerable<CategoryHistoryEntry>> GetCategoryHistoryByUserAsync(long userId);
    }
}
