using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    public enum InsightRuleFrequency
    {
        Diaria,
        Semanal,
        Mensal
    }

    public class InsightCandidate
    {
        public string RuleCode { get; init; } = string.Empty;
        public BehavioralInsightTypes Type { get; init; }
        public long? CategoryId { get; init; }
        public long? RelatedEntityId { get; init; }
        public DateOnly ReferenceDate { get; init; }
        public int CooldownDays { get; init; }
        public bool Once { get; init; }
        public string Subject { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Goal { get; init; } = string.Empty;
        public IReadOnlyList<string> Facts { get; init; } = [];
        public string FactsText => string.Join("\n", Facts);
        public string Key => InsightKey(RuleCode, CategoryId, RelatedEntityId);

        public static string InsightKey(string ruleCode, long? categoryId, long? relatedEntityId) =>
            $"{ruleCode}|{categoryId}|{relatedEntityId}";
    }
}
