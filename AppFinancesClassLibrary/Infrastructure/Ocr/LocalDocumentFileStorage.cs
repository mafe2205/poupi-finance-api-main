using AppFinancesClassLibrary.Application.Documents.Services;
using AppFinancesClassLibrary.Application.Options;
using Microsoft.Extensions.Options;

namespace AppFinancesClassLibrary.Infrastructure.Ocr
{
    public class LocalDocumentFileStorage(IOptions<OcrOptions> ocrOptions) : IDocumentFileStorage
    {
        private readonly OcrOptions ocrOptions = ocrOptions.Value;

        public async Task<string> SaveAsync(long userId, long documentId, byte[] content, CancellationToken cancellationToken = default)
        {
            var directory = Path.Combine(ocrOptions.StorageBasePath, userId.ToString());
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, $"{documentId}.jpg");
            await File.WriteAllBytesAsync(path, content, cancellationToken);

            return path;
        }
    }
}
