using AppFinancesClassLibrary.Application.WeeklyProfileMetric.Entities.Dtos;

namespace AppFinancesClassLibrary.Application.WeeklyProfileMetric.Services
{
    public interface IWeeklyProfileMetricService
    {
        Task<WeeklyProfileMetricResult> CreateAsync(CreateWeeklyProfileMetricDto dto);
        Task<WeeklyProfileMetricResult> GetByIdAsync(long id);
        Task UpdateAsync(UpdateWeeklyProfileMetricDto dto);
        Task<IEnumerable<WeeklyProfileMetricResult>> GetWeeklyMetricsAsync(long userId, string startWeek, string endWeek);
    }
}
