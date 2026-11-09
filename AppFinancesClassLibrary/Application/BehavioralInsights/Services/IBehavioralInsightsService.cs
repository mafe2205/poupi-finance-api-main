using AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Dtos;
using AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Results;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Services
{
    public interface IBehavioralInsightsService
    {
        Task<long> CreateAsync(CreateBehavioralInsightDto dto);
        Task<BehavioralInsightResult> GetByIdAsync(long id);
        Task<IEnumerable<BehavioralInsightResult>> GetAllByUserIdAsync(long userId, bool includeOutdated = false);
        Task UpdateAsync(long id, UpdateBehavioralInsightDto dto);
        Task MarkViewedAsync(long id);
        Task UpdateFeedbackAsync(long id, UpdateBehavioralInsightFeedbackDto dto);
        Task EvaluateActionTakenAsync(long id);
        Task<bool> DeleteAsync(long id);
    }
}
