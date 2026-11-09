using AppFinancesClassLibrary.Application.Reports.Entities.Results;

namespace AppFinancesClassLibrary.Application.Reports.Services
{
    public interface IReportsService
    {
        Task<ReportSummaryResult> GetSummaryAsync(DateOnly startDate, DateOnly endDate);
    }
}
