using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Services
{
    public interface IInsightTriggerService
    {
        Task<IReadOnlyList<InsightCandidate>> DetectAsync(long userId, InsightRuleFrequency frequency, DateOnly today);
        Task<IReadOnlyList<InsightCandidate>> EvaluateCurrentAsync(long userId, DateOnly today);
    }
}
