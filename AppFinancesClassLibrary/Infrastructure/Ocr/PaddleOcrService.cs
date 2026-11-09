using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Application.Documents.Services;

namespace AppFinancesClassLibrary.Infrastructure.Ocr
{
    public class PaddleOcrService(PaddleOcrEngine engine) : IOcrService
    {
        private readonly PaddleOcrEngine engine = engine;

        public Task<OcrExtractionResult> ExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            return engine.ExtractAsync(imageBytes, cancellationToken);
        }
    }
}
