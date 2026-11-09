namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public interface IDocumentFileStorage
    {
        Task<string> SaveAsync(long userId, long documentId, byte[] content, CancellationToken cancellationToken = default);
    }
}
