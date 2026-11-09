using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Services
{
    public interface IInsightRefreshService
    {
        Task<IReadOnlyList<OutdatedInsight>> RefreshAsync(long userId, DateOnly today, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<OutdatedInsight>> RefreshCurrentUserAsync(CancellationToken cancellationToken = default);
    }
}
