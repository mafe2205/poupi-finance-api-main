using AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IBehavioralInsightsRepository
    {
        Task<BehavioralInsight> GetByIdAsync(long id);
        Task<IEnumerable<BehavioralInsight>> GetAllByUserIdAsync(long userId, bool includeOutdated = false);
        Task<IEnumerable<BehavioralInsight>> GetCurrentRuleInsightsAsync(long userId);
        Task MarkOutdatedAsync(long id, string reason);
        Task<long> CreateAsync(CreateBehavioralInsightDto dto);
        Task<BehavioralInsight> UpdateAsync(UpdateBehavioralInsightDto dto);
        Task MarkViewedAsync(UpdateBehavioralInsightViewedAsync dto);
        Task<BehavioralInsight> UpdateFeedbackAsync(long Id, Feedback feedback);
        Task<IEnumerable<BehavioralInsight>> GetOverlappingAsync(long userId, long categoryId, DateTime windowStart, DateTime windowEnd, long excludeId);
        Task MarkAsNonEvaluableAsync(IEnumerable<long> insightIds);
        Task<IEnumerable<long>> GetPendingActionTakenEvaluationAsync(DateTime viewedBefore);
        Task<int> CountGeneratedInPeriodAsync(long userId, DateOnly from, DateOnly to);
        Task<int> CountInteractedInPeriodAsync(long userId, DateOnly from, DateOnly to);
        Task<bool> DeleteAsync(long id);
    }
}
