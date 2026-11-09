using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Services
{
    public interface IInsightGenerationService
    {
        Task GenerateAsync(long userId, InsightRuleFrequency frequency, DateOnly today, CancellationToken cancellationToken = default,
            IReadOnlySet<string> silentKeys = null);
    }
}
