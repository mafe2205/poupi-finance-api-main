using AppFinancesClassLibrary.Application.Options;
using AppFinancesClassLibrary.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    public interface IInsightRule
    {
        InsightRuleFrequency Frequency { get; }
        InsightHistoryTier HistoryTier => InsightHistoryTier.PrimeiroDia;

        IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx);
    }

    public static class InsightRuleEngine
    {
        public static IReadOnlyList<IInsightRule> AllRules { get; } =
        [
            new CategoryLimitExceededRule(),
            new WeekOverWeekIncreaseRule(),
            new BudgetEarlyWarningRule(),
            new BudgetUndersizedRule(),
            new CategoryWithoutLimitRule(),

            // Metas
            new GoalBehindScheduleRule(),
            new GoalDeadlineRiskRule(),
            new GoalRequiredContributionRule(),
            new GoalNearlyReachedRule(),
            new GoalReachedRule(),
            new SpendingCompetesWithGoalRule(),

            // Cartões
            new StatementNearLimitRule(),
            new StatementDueSoonRule(),
            new StatementGrowthRule(),
            new StatementProjectionRule(),
            new CardConcentrationRule(),
            new UnusedCardRule(),

            // Contas
            new LowBalanceRule(),
            new InsufficientBalanceForStatementRule(),
            new IdleBalanceRule(),

            // Fluxo
            new SpendingAboveIncomeRule(),
            new LowSavingsRateRule(),
            new IncomeDropRule(),
            new OutlierExpenseRule(),
            new RecurringExpenseRule(),
            new SpendingConcentrationRule(),
            new CriticalWeekdayRule(),
            new SpendingDropRule(),

            // Uso do app
            new PendingDocumentsRule(),
            new UncategorizedTransactionsRule(),
            new NoRecordsRule(),
        ];

        public static IReadOnlyList<InsightCandidate> Evaluate(
            InsightUserData data,
            InsightRuleOptions options,
            InsightRuleFrequency frequency,
            DateOnly today,
            IEnumerable<IInsightRule> rules = null,
            bool applyDedupe = true)
        {
            var ctx = new InsightRuleContext(data, options, today);
            var history = applyDedupe
                ? data.History.Select(h => new DedupeEntry(h.RuleCode, h.CategoryId, h.RelatedEntityId, SubjectOf(h), h.ReferenceDate)).ToList()
                : [];
            var accepted = new List<InsightCandidate>();

            var skippedForHistory = false;

            foreach (var rule in (rules ?? AllRules).Where(r => r.Frequency == frequency))
            {
                if (!HasHistoryFor(ctx, rule.HistoryTier))
                {
                    skippedForHistory = true;
                    continue;
                }

                foreach (var candidate in rule.Evaluate(ctx))
                    Accept(candidate);
            }

            if (skippedForHistory && frequency == InsightRuleFrequency.Diaria && !HasEnoughHistory(ctx))
                Accept(InsufficientDataCandidate(ctx));

            return accepted;

            void Accept(InsightCandidate candidate)
            {
                if (history.Any(h => Blocks(h, candidate))) return;

                accepted.Add(candidate);
                history.Add(new DedupeEntry(candidate.RuleCode, candidate.CategoryId, candidate.RelatedEntityId, candidate.Subject, candidate.ReferenceDate));
            }
        }

        public static bool HasEnoughHistory(InsightRuleContext ctx) => HasHistoryFor(ctx, InsightHistoryTier.DuasSemanas);

        public static bool HasHistoryFor(InsightRuleContext ctx, InsightHistoryTier tier) =>
            tier == InsightHistoryTier.PrimeiroDia
            || ctx.HasHistorySince(ctx.Today.AddDays(-Math.Max(0, RequiredHistoryDays(ctx.Options, tier))));

        public static int RequiredHistoryDays(InsightRuleOptions options, InsightHistoryTier tier) => tier switch
        {
            InsightHistoryTier.DuasSemanas => options.MinHistoryDays,
            InsightHistoryTier.UmMes => options.OneMonthHistoryDays,
            InsightHistoryTier.DoisMeses => options.TwoMonthsHistoryDays,
            _ => 0
        };

        private static InsightCandidate InsufficientDataCandidate(InsightRuleContext ctx)
        {
            var since = ctx.Data.FirstTransactionDate is { } first
                ? $"primeira transação registrada em: {InsightText.Date(first)}"
                : "nenhuma transação registrada ainda";
            var availableFrom = (ctx.Data.FirstTransactionDate ?? ctx.Today).AddDays(Math.Max(0, ctx.Options.MinHistoryDays));

            return new InsightCandidate
            {
                RuleCode = InsightRuleCodes.DadosInsuficientes,
                Type = BehavioralInsightTypes.sugestao,
                ReferenceDate = ctx.Today,
                Once = true,
                Title = "Ainda estamos conhecendo seus gastos",
                Goal = "explicar que ainda não há dados suficientes para analisar padrões de gasto e incentivar o usuário a continuar registrando as transações, sem tirar nenhuma conclusão sobre os gastos dele",
                Facts =
                [
                    since,
                    $"dias de histórico necessários para as análises de padrão: {ctx.Options.MinHistoryDays}",
                    $"as análises de padrão começam a partir de: {InsightText.Date(availableFrom)}"
                ]
            };
        }

        private static string SubjectOf(InsightHistoryEntry entry) =>
            entry.RuleCode == InsightRuleCodes.GastoRecorrente ? InsightText.NormalizeDescription(entry.RelatedDescription) : null;

        private static bool Blocks(DedupeEntry existing, InsightCandidate candidate)
        {
            if (existing.RuleCode != candidate.RuleCode || existing.CategoryId != candidate.CategoryId) return false;

            var sameSubject = candidate.Subject is not null
                ? existing.Subject == candidate.Subject
                : existing.RelatedEntityId == candidate.RelatedEntityId;
            if (!sameSubject) return false;

            if (candidate.Once || existing.ReferenceDate == candidate.ReferenceDate) return true;

            return candidate.CooldownDays > 0
                && Math.Abs(candidate.ReferenceDate.DayNumber - existing.ReferenceDate.DayNumber) < candidate.CooldownDays;
        }

        private sealed record DedupeEntry(string RuleCode, long? CategoryId, long? RelatedEntityId, string Subject, DateOnly ReferenceDate);
    }
}
