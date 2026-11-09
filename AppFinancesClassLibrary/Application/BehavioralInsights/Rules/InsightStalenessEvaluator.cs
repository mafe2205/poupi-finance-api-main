using AppFinancesClassLibrary.Application.Options;
using AppFinancesClassLibrary.Domain.Entities;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    public enum InsightOutdatedReason
    {
        dados_alterados,
        nao_se_aplica
    }

    public record OutdatedInsight(long InsightId, string Key, InsightOutdatedReason Reason);

    public static class InsightStalenessEvaluator
    {
        private static readonly HashSet<string> PeriodlessRules =
        [
            InsightRuleCodes.MetaQuaseAtingida,
            InsightRuleCodes.MetaConcluida,
            InsightRuleCodes.DadosInsuficientes,
        ];

        public static IReadOnlyList<OutdatedInsight> FindOutdated(
            IEnumerable<BehavioralInsight> currentInsights,
            IReadOnlyList<InsightCandidate> candidatesNow,
            DateOnly today)
        {
            var ctx = new InsightRuleContext(new InsightUserData(), new InsightRuleOptions(), today);
            var periodsEvaluatedToday = new HashSet<DateOnly> { today, ctx.CurrentMonthStart, ctx.LastClosedWeekEnd, ctx.LastClosedMonthStart };

            var outdated = new List<OutdatedInsight>();

            foreach (var insight in currentInsights.Where(i => i.RuleCode is not null && i.OutdatedAt is null))
            {
                var key = InsightCandidate.InsightKey(insight.RuleCode, insight.CategoryId, insight.RelatedEntityId);
                var periodless = PeriodlessRules.Contains(insight.RuleCode);

                var match = candidatesNow.FirstOrDefault(c =>
                    c.Key == key && (periodless || c.ReferenceDate == insight.ReferenceDate));

                if (match is not null)
                {
                    if (insight.Facts is not null && insight.Facts != match.FactsText)
                        outdated.Add(new OutdatedInsight(insight.Id, key, InsightOutdatedReason.dados_alterados));
                    continue;
                }

                var evaluatedToday = periodless
                    || periodsEvaluatedToday.Contains(insight.ReferenceDate)
                    || candidatesNow.Any(c => c.RuleCode == insight.RuleCode && c.ReferenceDate == insight.ReferenceDate);

                if (evaluatedToday)
                    outdated.Add(new OutdatedInsight(insight.Id, key, InsightOutdatedReason.nao_se_aplica));
            }

            return outdated;
        }
    }
}
