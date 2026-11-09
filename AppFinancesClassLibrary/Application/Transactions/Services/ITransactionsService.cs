using AppFinancesClassLibrary.Application.Documents.Entities.Dtos;
using AppFinancesClassLibrary.Application.Transactions.Entities.Dtos;
using AppFinancesClassLibrary.Application.Transactions.Entities.Results;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.Transactions.Services
{
    public interface ITransactionsService
    {
        Task<Transaction> CreateAsync(CreateTransactionDto dto);
        Task<Transaction> CreateFromDocumentAsync(long documentId, ConfirmDocumentDto dto);
        Task<Transaction> GetByIdAsync(long id);
        Task<IEnumerable<Transaction>> GetByUserAsync(TransactionFilterDto filter);
        Task<Transaction> UpdateAsync(long id, UpdateTransactionDto dto);
        Task<Transaction> DeleteAsync(long id);
        Task CancelSeriesAsync(long transactionId);
        Task<ReclassificationResult> ReclassifyAsync();
    }
}
