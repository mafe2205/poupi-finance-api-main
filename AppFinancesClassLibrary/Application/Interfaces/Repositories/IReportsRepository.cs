using AppFinancesClassLibrary.Application.Reports.Entities;
using AppFinancesClassLibrary.Application.Reports.Entities.Results;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IReportsRepository
    {
        Task<IEnumerable<ReportTransactionRow>> GetTransactionsAsync(long userId, DateOnly from, DateOnly to);
        Task<IEnumerable<ReportBalanceMovementRow>> GetBalanceMovementsAfterAsync(long userId, DateOnly after);
        Task<long> GetCurrentBalanceAsync(long userId);
        Task<IEnumerable<ReportCard>> GetActiveCardsAsync(long userId);
        Task<IEnumerable<ReportInvoiceGroup>> GetInvoicesByStatusAsync(long userId, DateOnly from, DateOnly to);
    }
}
