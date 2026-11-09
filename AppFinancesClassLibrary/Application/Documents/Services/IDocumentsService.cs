using AppFinancesClassLibrary.Application.Documents.Entities.Dtos;
using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public interface IDocumentsService
    {
        Task<long> CreateAsync(CreateDocumentDto dto);
        Task<DocumentResult> GetByIdAsync(long id);
        Task<IEnumerable<DocumentResult>> GetAllByUserIdAsync(long userId);
        Task<DocumentResult> LinkTransactionAsync(long id, long transactionId);
        Task<DocumentResult> UpdateStatusAsync(long id, StatusOCRTypes status);
        Task<DocumentResult> ConfirmManuallyAsync(long id, bool confirmedManually);
        Task<DocumentResult> DeleteAsync(long id);
    }
}
