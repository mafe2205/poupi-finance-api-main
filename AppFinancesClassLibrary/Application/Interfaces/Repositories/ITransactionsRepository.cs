using AppFinancesClassLibrary.Application.Transactions.Entities.Dtos;
using AppFinancesClassLibrary.Application.Transactions.Entities.Results;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface ITransactionsRepository
    {
        Task<Transaction> GetByIdAsync(long id);
        Task<IEnumerable<Transaction>> GetByUserAsync(long userId, TransactionFilterDto filter);
        Task<int> GetTotalByCategoryAndPeriodAsync(GetCategoryTotalsByPeriodDto dto);
        Task<IEnumerable<CategoryPeriodTotal>> GetCategoryTotalsByPeriodAsync(long userId, DateOnly from, DateOnly to);
        Task<IEnumerable<Transaction>> GetBySerialIdAsync(long recorrentSerialId);
        Task<long> NextRecorrentSerialIdAsync();
        Task<Transaction> CreateAsync(CreateTransactionInternalDto dto);
        Task UpdateAsync(UpdateTransactionInternalDto dto);
        Task<IEnumerable<Transaction>> GetAutoClassifiedByUserAsync(long userId);
        Task<bool> UpdateAutoCategoryAsync(long id, long categoryId);
        Task<DateTime> SoftDeleteAsync(SoftDeleteDto dto);
        Task SetPaymentDateAsync(long id, DateOnly paymentDate);
        Task<int> GetTotalByTypeAndPeriodAsync(long userId, CategoriesTypes tipo, DateOnly from, DateOnly to);
        Task<int> CountLateAsync(long userId, DateOnly from, DateOnly to);
    }
}