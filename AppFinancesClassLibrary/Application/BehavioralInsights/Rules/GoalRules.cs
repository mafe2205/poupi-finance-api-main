using AppFinancesClassLibrary.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using static AppFinancesClassLibrary.Application.BehavioralInsights.Rules.InsightText;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    internal static class GoalRuleHelpers
    {
        public static int DaysLeft(this InsightGoal goal, DateOnly today) => goal.FinalDate.DayNumber - today.DayNumber;

        public static int MonthsLeft(this InsightGoal goal, DateOnly today) => Math.Max(1, (int)Math.Ceiling(goal.DaysLeft(today) / 30d));

        public static long RequiredMonthlyCents(this InsightGoal goal, DateOnly today) =>
            (long)Math.Ceiling((decimal)goal.RemainingCents / goal.MonthsLeft(today));

        public static bool IsInProgress(this InsightGoal goal, DateOnly today) =>
            goal.ProgressPercent < 100m && goal.FinalDate > today;
    }

    public class GoalBehindScheduleRule : IInsightRule
    {
        private const int MinGoalAgeDays = 14;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            foreach (var goal in ctx.Data.Goals)
            {
                var totalDays = goal.FinalDate.DayNumber - goal.InicialDate.DayNumber;
                var elapsedDays = ctx.Today.DayNumber - goal.InicialDate.DayNumber;
                if (totalDays <= 0 || elapsedDays < MinGoalAgeDays || ctx.Today > goal.FinalDate) continue;

                var progress = goal.ProgressPercent;
                var elapsed = elapsedDays * 100m / totalDays;
                if (progress >= 100m || progress >= elapsed - ctx.Options.Goals.Atraso.FolgaPp) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.MetaAtrasada,
                    Type = BehavioralInsightTypes.alerta_gasto,
                    RelatedEntityId = goal.Id,
                    ReferenceDate = ctx.Today,
                    CooldownDays = 14,
                    Title = Title($"Meta atrasada: {goal.Description}"),
                    Goal = "avisar que o usuário guardou menos do que o prazo da meta pedia até agora",
                    Facts =
                    [
                        $"meta: {goal.Description}",
                        $"progresso da meta: {Percent(progress)}",
                        $"prazo já decorrido: {Percent(elapsed)}",
                        $"valor que falta: {Money(goal.RemainingCents)}",
                        $"dias restantes: {goal.DaysLeft(ctx.Today)}"
                    ]
                };
            }
        }
    }

    public class GoalDeadlineRiskRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DuasSemanas;

        private const int SavingsMonths = 2;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            if (!ctx.HasClosedMonths(SavingsMonths)) yield break;

            var monthlySavings = ctx.AverageMonthlySavings(SavingsMonths);

            foreach (var goal in ctx.Data.Goals)
            {
                var daysLeft = goal.DaysLeft(ctx.Today);
                if (daysLeft < 0 || daysLeft > ctx.Options.Goals.Vencimento.DiasAlerta || goal.RemainingCents <= 0) continue;

                var reachable = monthlySavings * daysLeft / 30m;
                if (goal.RemainingCents <= reachable) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.MetaRiscoPrazo,
                    Type = BehavioralInsightTypes.alerta_gasto,
                    RelatedEntityId = goal.Id,
                    ReferenceDate = ctx.Today,
                    CooldownDays = 7,
                    Title = Title($"Meta em risco: {goal.Description}"),
                    Goal = "avisar que, no ritmo atual de poupança, o usuário não vai conseguir fechar o valor da meta até o prazo",
                    Facts =
                    [
                        $"meta: {goal.Description}",
                        $"valor que falta: {Money(goal.RemainingCents)}",
                        $"dias restantes: {daysLeft}",
                        $"poupança mensal média: {Money(monthlySavings)}",
                        $"quanto vai faltar no prazo: {Money(goal.RemainingCents - Math.Max(0m, reachable))}"
                    ]
                };
            }
        }
    }

    public class GoalRequiredContributionRule : IInsightRule
    {
        private const int MinGoalAgeDays = 30;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Semanal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var idleSince = ctx.Today.AddDays(-ctx.Options.Goals.Aporte.DiasSemAporte);

            foreach (var goal in ctx.Data.Goals)
            {
                if (!goal.IsInProgress(ctx.Today)) continue;
                if (ctx.Today.DayNumber - goal.InicialDate.DayNumber < MinGoalAgeDays) continue;
                if (goal.LastDepositDate is { } lastDeposit && lastDeposit > idleSince) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.AporteNecessario,
                    Type = BehavioralInsightTypes.sugestao,
                    RelatedEntityId = goal.Id,
                    ReferenceDate = ctx.LastClosedWeekEnd,
                    CooldownDays = 30,
                    Title = Title($"Retome a meta {goal.Description}"),
                    Goal = "sugerir quanto o usuário precisa guardar por mês para chegar na meta no prazo, já que ela está sem aportes",
                    Facts =
                    [
                        $"meta: {goal.Description}",
                        $"aporte mensal necessário: {Money(goal.RequiredMonthlyCents(ctx.Today))}",
                        $"meses restantes: {goal.MonthsLeft(ctx.Today)}"
                    ]
                };
            }
        }
    }

    public class GoalNearlyReachedRule : IInsightRule
    {
        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            foreach (var goal in ctx.Data.Goals)
            {
                var progress = goal.ProgressPercent;
                if (progress < ctx.Options.Goals.Proxima.PercentualAviso || progress >= 100m) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.MetaQuaseAtingida,
                    Type = BehavioralInsightTypes.sugestao,
                    RelatedEntityId = goal.Id,
                    ReferenceDate = ctx.Today,
                    Once = true,
                    Title = Title($"Falta pouco: {goal.Description}"),
                    Goal = "incentivar o usuário, porque a meta está quase completa",
                    Facts =
                    [
                        $"meta: {goal.Description}",
                        $"progresso: {Percent(progress)}",
                        $"valor que falta: {Money(goal.RemainingCents)}"
                    ]
                };
            }
        }
    }

    public class GoalReachedRule : IInsightRule
    {
        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            foreach (var goal in ctx.Data.Goals.Where(g => g.ProgressPercent >= 100m))
            {
                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.MetaConcluida,
                    Type = BehavioralInsightTypes.sugestao,
                    RelatedEntityId = goal.Id,
                    ReferenceDate = ctx.Today,
                    Once = true,
                    Title = Title($"Meta concluída: {goal.Description}"),
                    Goal = "parabenizar o usuário pela meta concluída e sugerir criar a próxima meta",
                    Facts =
                    [
                        $"meta: {goal.Description}",
                        $"valor atingido: {Money(goal.SavedCents)}",
                        $"data de conclusão: {Date(goal.LastDepositDate ?? ctx.Today)}"
                    ]
                };
            }
        }
    }

    public class SpendingCompetesWithGoalRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DuasSemanas;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Semanal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            if (!ctx.HasHistorySince(ctx.PreviousWeekStart)) yield break;

            var current = ctx.ExpensesByCategory(ctx.LastClosedWeekStart, ctx.LastClosedWeekEnd);
            var previous = ctx.ExpensesByCategory(ctx.PreviousWeekStart, ctx.PreviousWeekEnd);

            var biggest = current
                .Select(c => (CategoryId: c.Key, Increase: c.Value - previous.GetValueOrDefault(c.Key)))
                .Where(c => c.Increase > 0 && !(ctx.Category(c.CategoryId)?.IsDefault ?? true))
                .OrderByDescending(c => c.Increase)
                .FirstOrDefault();
            if (biggest.Increase <= 0) yield break;

            var category = ctx.Category(biggest.CategoryId);

            foreach (var goal in ctx.Data.Goals.Where(g => g.IsInProgress(ctx.Today)))
            {
                var required = goal.RequiredMonthlyCents(ctx.Today);
                if (biggest.Increase <= ctx.Options.Goals.Concorrencia.FracaoAporte * required) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.GastoConcorreMeta,
                    Type = BehavioralInsightTypes.padrao_detectado,
                    CategoryId = category.Id,
                    RelatedEntityId = goal.Id,
                    ReferenceDate = ctx.LastClosedWeekEnd,
                    CooldownDays = 30,
                    Title = Title($"{category.Name} x meta {goal.Description}"),
                    Goal = "apontar que o aumento de gasto na categoria compete com o aporte que a meta exige",
                    Facts =
                    [
                        $"categoria: {category.Name}",
                        $"aumento do gasto na semana: {Money(biggest.Increase)}",
                        $"meta: {goal.Description}",
                        $"aporte mensal necessário para a meta: {Money(required)}"
                    ]
                };
            }
        }
    }
}
