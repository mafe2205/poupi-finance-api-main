namespace AppFinancesClassLibrary.Application.WeeklyMetrics.Services
{
    public interface IWeeklyMetricsService
    {
        Task CalculateForUserAndWeekAsync(long userId, DateOnly anyDateInWeek);
        Task BackfillForUserAsync(long userId, DateOnly fromDate, DateOnly toDate);
    }
}
