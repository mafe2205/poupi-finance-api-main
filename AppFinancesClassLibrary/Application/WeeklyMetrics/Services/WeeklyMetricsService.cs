using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.WeeklyProfileMetric.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Enums;
using System.Globalization;

namespace AppFinancesClassLibrary.Application.WeeklyMetrics.Services
{
     public class WeeklyMetricsService(ITransactionsRepository transactionRepository, IBehavioralInsightsRepository behavioralInsightsRepository, IWeeklyProfileMetricRepository weeklyProfileMetricRepository) : IWeeklyMetricsService
    {
        private readonly ITransactionsRepository transactionRepository = transactionRepository;
        private readonly IBehavioralInsightsRepository behavioralInsightsRepository = behavioralInsightsRepository;
        private readonly IWeeklyProfileMetricRepository weeklyProfileMetricRepository = weeklyProfileMetricRepository;

        public async Task CalculateForUserAndWeekAsync(long userId, DateOnly anyDateInWeek)
        {
            var (weekStart, weekEnd) = GetIsoWeekRange(anyDateInWeek);
            var weekReference = GetIsoWeekReference(anyDateInWeek);

            var totalExpenses = await transactionRepository.GetTotalByTypeAndPeriodAsync(userId, CategoriesTypes.Saida, weekStart, weekEnd);

            var totalRevenue = await transactionRepository.GetTotalByTypeAndPeriodAsync(userId, CategoriesTypes.Entrada, weekStart, weekEnd);

            var lateTransactions = await transactionRepository.CountLateAsync(userId, weekStart, weekEnd);

            var insightsGenerated = await behavioralInsightsRepository.CountGeneratedInPeriodAsync(userId, weekStart, weekEnd);

            var insightsInteracted = await behavioralInsightsRepository.CountInteractedInPeriodAsync(userId, weekStart, weekEnd);

            decimal percentualGasto = totalRevenue > 0
                ? Math.Round((decimal)totalExpenses / totalRevenue * 100, 3)
                : 0;

            CreateWeeklyProfileMetricDto metric = new()
            {
                UserId = userId,
                WeekReference = weekReference,
                TotalExpensesCents = totalExpenses,
                TotalRevenueCents = totalRevenue,
                PercentageExpenseRelativeRevenue = percentualGasto,
                QuantityLateTransactions = lateTransactions,
                QuantityInsightsGenerated = insightsGenerated,
                QuantityInsightsInteracted = insightsInteracted,
            };

            var existente = await weeklyProfileMetricRepository.GetByUserAndWeekAsync(userId, weekReference);

            if (existente is null)
                await weeklyProfileMetricRepository.CreateAsync(metric);
            else
                await weeklyProfileMetricRepository.UpdateAsync(new UpdateWeeklyProfileMetricDto
                {
                    Id = existente.Id,
                    UserId = metric.UserId,
                    WeekReference = metric.WeekReference,
                    TotalExpensesCents = metric.TotalExpensesCents,
                    TotalRevenueCents = metric.TotalRevenueCents,
                    PercentageExpenseRelativeRevenue = metric.PercentageExpenseRelativeRevenue,
                    QuantityLateTransactions = metric.QuantityLateTransactions,
                    QuantityInsightsGenerated = metric.QuantityInsightsGenerated,
                    QuantityInsightsInteracted = metric.QuantityInsightsInteracted,
                });
        }

        public async Task BackfillForUserAsync(long userId, DateOnly fromDate, DateOnly toDate)
        {
            var cursor = fromDate;
            while (cursor <= toDate)
            {
                await CalculateForUserAndWeekAsync(userId, cursor);
                cursor = cursor.AddDays(7);
            }
        }

        private static (DateOnly start, DateOnly end) GetIsoWeekRange(DateOnly date)
        {
            var dayOfWeek = (int)date.DayOfWeek;
            var diffToMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
            var monday = date.AddDays(-diffToMonday);
            var sunday = monday.AddDays(6);
            return (monday, sunday);
        }

        private static string GetIsoWeekReference(DateOnly date)
        {
            var dt = date.ToDateTime(TimeOnly.MinValue);
            var week = ISOWeek.GetWeekOfYear(dt);
            var year = ISOWeek.GetYear(dt);
            return $"{year}-W{week:D2}";
        }
    }
}
