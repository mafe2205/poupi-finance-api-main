using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Services
{
    public class InsightRefreshService(
        IInsightTriggerService triggerService,
        IBehavioralInsightsRepository behavioralInsightsRepository,
        IInsightGenerationService insightGenerationService,
        ICurrentUser currentUser) : IInsightRefreshService
    {
        public Task<IReadOnlyList<OutdatedInsight>> RefreshCurrentUserAsync(CancellationToken cancellationToken = default)
            => RefreshAsync(currentUser.UserId, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);

        public async Task<IReadOnlyList<OutdatedInsight>> RefreshAsync(long userId, DateOnly today, CancellationToken cancellationToken = default)
        {
            Domain.Validate.Id(userId, nameof(userId));

            var candidatesNow = await triggerService.EvaluateCurrentAsync(userId, today);
            var current = await behavioralInsightsRepository.GetCurrentRuleInsightsAsync(userId);
            var outdated = InsightStalenessEvaluator.FindOutdated(current, candidatesNow, today);

            foreach (var insight in outdated)
                await behavioralInsightsRepository.MarkOutdatedAsync(insight.InsightId, insight.Reason.ToString());

            var replacedKeys = outdated
                .Where(o => o.Reason == InsightOutdatedReason.dados_alterados)
                .Select(o => o.Key)
                .ToHashSet();

            foreach (var frequency in Enum.GetValues<InsightRuleFrequency>())
                await insightGenerationService.GenerateAsync(userId, frequency, today, cancellationToken, replacedKeys);

            return outdated;
        }
    }
}
