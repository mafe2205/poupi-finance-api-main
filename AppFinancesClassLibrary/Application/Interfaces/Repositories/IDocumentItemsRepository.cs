using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IDocumentItemsRepository
    {
        Task<IEnumerable<DocumentItem>> GetByDocumentIdAsync(long documentId);
        Task<IEnumerable<DocumentItem>> GetByDocumentIdsAsync(IEnumerable<long> documentIds);
        Task ReplaceForDocumentAsync(long documentId, IEnumerable<ParsedDocumentItemResult> items);
    }
}
