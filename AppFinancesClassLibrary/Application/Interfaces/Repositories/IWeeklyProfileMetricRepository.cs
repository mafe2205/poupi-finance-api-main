using AppFinancesClassLibrary.Application.WeeklyProfileMetric.Entities.Dtos;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IWeeklyProfileMetricRepository
    {
        Task<Domain.Entities.WeeklyProfileMetric> GetByIdAsync(long id);
        Task<Domain.Entities.WeeklyProfileMetric> CreateAsync(CreateWeeklyProfileMetricDto dto);
        Task UpdateAsync(UpdateWeeklyProfileMetricDto dto);
        Task<IEnumerable<WeeklyProfileMetricResult>> GetWeeklyMetricsAsync(long userId, string startWeek, string endWeek);
        Task<WeeklyProfileMetricResult> GetByUserAndWeekAsync(long userId, string weekReference);
    }
}
