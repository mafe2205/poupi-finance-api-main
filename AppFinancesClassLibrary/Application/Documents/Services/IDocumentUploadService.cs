using AppFinancesClassLibrary.Application.Documents.Entities.Results;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public interface IDocumentUploadService
    {
        Task<DocumentUploadResult> UploadAsync(Stream imageStream, string? idempotencyKey = null, CancellationToken cancellationToken = default);
        Task ProcessAsync(long documentId, byte[] imageBytes, CancellationToken cancellationToken = default);
    }
}
