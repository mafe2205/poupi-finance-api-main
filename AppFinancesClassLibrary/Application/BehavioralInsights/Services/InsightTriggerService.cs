using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Application.Categories.Entities.Results;
using AppFinancesClassLibrary.Application.Documents.Services;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Options;
using Microsoft.Extensions.Options;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Services
{
    public class InsightTriggerService(
        IInsightDataRepository insightDataRepository,
        IDocumentParsingService documentParsingService,
        IOptions<InsightRuleOptions> ruleOptions) : IInsightTriggerService
    {
        private const int TransactionWindowDays = 190;

        private readonly InsightRuleOptions ruleOptions = ruleOptions.Value;

        public async Task<IReadOnlyList<InsightCandidate>> DetectAsync(long userId, InsightRuleFrequency frequency, DateOnly today)
        {
            var data = await LoadAsync(userId, today);
            return InsightRuleEngine.Evaluate(data, ruleOptions, frequency, today);
        }

        public async Task<IReadOnlyList<InsightCandidate>> EvaluateCurrentAsync(long userId, DateOnly today)
        {
            var data = await LoadAsync(userId, today);
            return Enum.GetValues<InsightRuleFrequency>()
                .SelectMany(frequency => InsightRuleEngine.Evaluate(data, ruleOptions, frequency, today, applyDedupe: false))
                .ToList();
        }

        private async Task<InsightUserData> LoadAsync(long userId, DateOnly today)
        {
            var data = await insightDataRepository.LoadAsync(userId, today, today.AddDays(-TransactionWindowDays));

            foreach (var document in data.PendingDocuments)
                document.ExtractedValueCents = documentParsingService.Parse(document.TextOcr, Array.Empty<CategoryResult>()).PaymentValueCents;

            return data;
        }
    }
}
