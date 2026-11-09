using AppFinancesClassLibrary.Application.Documents.Entities.Results;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public interface IOcrService
    {
        Task<OcrExtractionResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
    }
}
