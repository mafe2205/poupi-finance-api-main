using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface IInsightDataRepository
    {
        Task<InsightUserData> LoadAsync(long userId, DateOnly today, DateOnly transactionsFrom);
    }
}
