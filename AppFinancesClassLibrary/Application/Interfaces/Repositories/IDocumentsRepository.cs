using AppFinancesClassLibrary.Application.Documents.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IDocumentsRepository
    {
        Task<Document> GetByIdAsync(long id);
        Task<IEnumerable<Document>> GetAllByUserIdAsync(long userId);
        Task<Document?> CreateAsync(CreateDocumentDto dto);
        Task<Document> UpdateTransactionAsync(LinkTransactionDto dto);
        Task<Document?> GetByIdempotencyKeyAsync(GetByIdempotencyKeyDto dto);
        Task<bool> TryLinkTransactionAsync(LinkTransactionDto dto);
        Task<Document> UpdateStatusAsync(UpdateStatusDto dto);
        Task<Document> UpdateDocumentUrlAsync(UpdateDocumentUrlDto dto);
        Task<Document> UpdateOcrResultAsync(UpdateOcrResultDto dto);
        Task MarkOcrStartedAsync(long id, DateTime ocrStartedAt);
        Task<Document> UpdateConfirmedManuallyAsync(ConfirmManuallyDto dto);
        Task<bool> DeleteAsync(long id);
    }
}
