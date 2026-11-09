using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Reports.Entities.Results;
using AppFinancesClassLibrary.Domain.Exceptions;

namespace AppFinancesClassLibrary.Application.Reports.Services
{
    public class ReportsService(IReportsRepository reportsRepository, ICurrentUser currentUser) : IReportsService
    {
        private readonly IReportsRepository reportsRepository = reportsRepository;

        public async Task<ReportSummaryResult> GetSummaryAsync(DateOnly startDate, DateOnly endDate)
        {
            long userId = currentUser.UserId;

            if (startDate == default || endDate == default)
                throw new DomainValidationException("startDate e endDate são obrigatórios, no formato YYYY-MM-DD.");
            if (startDate > endDate)
                throw new DomainValidationException("startDate não pode ser depois de endDate.");
            if (ReportSummaryBuilder.PeriodDays(startDate, endDate) > ReportSummaryBuilder.MaxPeriodDays)
                throw new DomainValidationException($"O período pode ter no máximo {ReportSummaryBuilder.MaxPeriodDays} dias.");

            var (previousStart, _) = ReportSummaryBuilder.PreviousPeriod(startDate, endDate);
            var firstBalancePoint = ReportSummaryBuilder.BalancePointDates(startDate, endDate)[0];

            var transactions = (await reportsRepository.GetTransactionsAsync(userId, previousStart, endDate)).ToList();
            var balanceMovements = (await reportsRepository.GetBalanceMovementsAfterAsync(userId, firstBalancePoint)).ToList();
            var currentBalance = await reportsRepository.GetCurrentBalanceAsync(userId);
            var cards = await reportsRepository.GetActiveCardsAsync(userId);
            var invoices = await reportsRepository.GetInvoicesByStatusAsync(userId, startDate, endDate);

            return ReportSummaryBuilder.Build(startDate, endDate, transactions, balanceMovements, currentBalance, cards, invoices);
        }
    }
}
