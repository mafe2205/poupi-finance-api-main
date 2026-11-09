using AppFinancesClassLibrary.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using static AppFinancesClassLibrary.Application.BehavioralInsights.Rules.InsightText;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    internal static class BudgetRuleHelpers
    {
        public static IEnumerable<InsightCategory> SpendingCategories(this InsightRuleContext ctx) =>
            ctx.Data.Categories.Where(c => c.Active && c.Type == CategoriesTypes.Saida && !c.IsDefault);
    }

    public class CategoryLimitExceededRule : IInsightRule
    {
        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var monthToDate = ctx.ExpensesByCategory(ctx.CurrentMonthStart, ctx.Today);

            foreach (var category in ctx.SpendingCategories().Where(c => c.LimitValueCents > 0))
            {
                var spent = monthToDate.GetValueOrDefault(category.Id);
                if (spent < ctx.Options.MinCategorySpendCentsToConsider) continue;

                var percent = spent * 100m / category.LimitValueCents;
                if (percent < ctx.Options.CategoryLimitThresholdPercent) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.LimiteCategoria,
                    Type = BehavioralInsightTypes.alerta_gasto,
                    CategoryId = category.Id,
                    ReferenceDate = ctx.CurrentMonthStart,
                    Title = Title($"Limite estourado: {category.Name}"),
                    Goal = "alertar que o gasto do mês na categoria atingiu ou passou do limite mensal definido",
                    Facts =
                    [
                        $"categoria: {category.Name}",
                        $"gasto no mês até agora: {Money(spent)}",
                        $"limite mensal: {Money(category.LimitValueCents)}",
                        $"percentual do limite: {Percent(percent)}"
                    ]
                };
            }
        }
    }

    public class WeekOverWeekIncreaseRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DuasSemanas;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Semanal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var current = ctx.ExpensesByCategory(ctx.LastClosedWeekStart, ctx.LastClosedWeekEnd);
            var previous = ctx.ExpensesByCategory(ctx.PreviousWeekStart, ctx.PreviousWeekEnd);

            foreach (var category in ctx.SpendingCategories())
            {
                var currentCents = current.GetValueOrDefault(category.Id);
                var previousCents = previous.GetValueOrDefault(category.Id);
                if (currentCents < ctx.Options.MinCategorySpendCentsToConsider || previousCents <= 0) continue;

                var increase = (currentCents - previousCents) * 100m / previousCents;
                if (increase < ctx.Options.WeekOverWeekIncreaseThresholdPercent) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.AumentoSemanal,
                    Type = BehavioralInsightTypes.sugestao,
                    CategoryId = category.Id,
                    ReferenceDate = ctx.LastClosedWeekEnd,
                    Title = Title($"Gasto em alta: {category.Name}"),
                    Goal = "comentar o aumento do gasto na categoria em relação à semana anterior e sugerir que o usuário preste atenção a esse gasto",
                    Facts =
                    [
                        $"categoria: {category.Name}",
                        $"gasto na semana: {Money(currentCents)}",
                        $"gasto na semana anterior: {Money(previousCents)}",
                        $"aumento: {Percent(increase)}"
                    ]
                };
            }
        }
    }

    public class BudgetEarlyWarningRule : IInsightRule
    {
        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var monthToDate = ctx.ExpensesByCategory(ctx.CurrentMonthStart, ctx.Today);
            var daysLeft = InsightRuleContext.MonthEnd(ctx.CurrentMonthStart).DayNumber - ctx.Today.DayNumber;

            foreach (var category in ctx.SpendingCategories().Where(c => c.LimitValueCents > 0))
            {
                var spent = monthToDate.GetValueOrDefault(category.Id);
                if (spent < ctx.Options.MinCategorySpendCentsToConsider) continue;

                var ratio = (decimal)spent / category.LimitValueCents;

                if (ratio < ctx.Options.Budget.Aviso.Nivel || ratio * 100m >= ctx.Options.CategoryLimitThresholdPercent) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.AvisoLimite,
                    Type = BehavioralInsightTypes.alerta_gasto,
                    CategoryId = category.Id,
                    ReferenceDate = ctx.CurrentMonthStart,
                    Title = Title($"Limite quase no fim: {category.Name}"),
                    Goal = "avisar com antecedência que o gasto do mês na categoria está perto do limite mensal",
                    Facts =
                    [
                        $"categoria: {category.Name}",
                        $"gasto acumulado no mês: {Money(spent)}",
                        $"limite mensal: {Money(category.LimitValueCents)}",
                        $"percentual usado: {Percent(ratio * 100m)}",
                        $"quanto resta do limite: {Money(category.LimitValueCents - spent)}",
                        $"dias restantes no mês: {daysLeft}"
                    ]
                };
            }
        }
    }

    public class BudgetUndersizedRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DoisMeses;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Mensal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var months = ctx.Options.Budget.Subdimensionado.MesesSeguidos;
            if (months < 1 || !ctx.HasClosedMonths(months)) yield break;

            var monthlyTotals = Enumerable.Range(1, months)
                .Select(i => ctx.ExpensesByCategory(ctx.ClosedMonthStart(i), InsightRuleContext.MonthEnd(ctx.ClosedMonthStart(i))))
                .ToList();

            foreach (var category in ctx.SpendingCategories().Where(c => c.LimitValueCents > 0))
            {
                var spent = monthlyTotals.Select(m => m.GetValueOrDefault(category.Id)).ToList();
                if (spent.Any(s => s <= category.LimitValueCents)) continue;

                var suggested = (long)Math.Ceiling(spent.Average() / 100d) * 100;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.LimiteSubdimensionado,
                    Type = BehavioralInsightTypes.sugestao,
                    CategoryId = category.Id,
                    ReferenceDate = ctx.LastClosedMonthStart,
                    CooldownDays = 60,
                    Title = Title($"Revise o limite de {category.Name}"),
                    Goal = "sugerir revisar o valor do limite mensal da categoria, que foi estourado mês após mês",
                    Facts =
                    [
                        $"categoria: {category.Name}",
                        $"limite atual: {Money(category.LimitValueCents)}",
                        $"gasto nos últimos {months} meses (do mais recente ao mais antigo): {string.Join(", ", spent.Select(Money))}",
                        $"limite sugerido: {Money(suggested)}"
                    ]
                };
            }
        }
    }

    public class CategoryWithoutLimitRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.UmMes;

        private const int MinExpenseTransactions = 10;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Mensal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var monthStart = ctx.LastClosedMonthStart;
            var monthEnd = InsightRuleContext.MonthEnd(monthStart);
            if (ctx.Expenses(monthStart, monthEnd).Count() < MinExpenseTransactions) yield break;

            var totals = ctx.ExpensesByCategory(monthStart, monthEnd);
            var monthTotal = totals.Values.Sum();
            var spendingCategories = ctx.SpendingCategories().ToDictionary(c => c.Id);

            var top = totals
                .Where(t => spendingCategories.ContainsKey(t.Key) && t.Value > 0)
                .OrderByDescending(t => t.Value)
                .Take(ctx.Options.Budget.SemLimite.TopN);

            foreach (var (categoryId, spent) in top)
            {
                var category = spendingCategories[categoryId];
                if (category.LimitValueCents > 0) continue;

                var average = Enumerable.Range(1, 3)
                    .Average(i => (decimal)ctx.Expenses(ctx.ClosedMonthStart(i), InsightRuleContext.MonthEnd(ctx.ClosedMonthStart(i)))
                        .Where(t => t.CategoryId == categoryId)
                        .Sum(t => (long)t.PaymentValueCents));

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.CategoriaSemLimite,
                    Type = BehavioralInsightTypes.sugestao,
                    CategoryId = categoryId,
                    ReferenceDate = monthStart,
                    CooldownDays = 90,
                    Title = Title($"Defina um limite para {category.Name}"),
                    Goal = "sugerir definir um limite mensal para uma das categorias em que o usuário mais gasta",
                    Facts =
                    [
                        $"categoria: {category.Name}",
                        $"gasto no mês passado: {Money(spent)}",
                        $"percentual do gasto total do mês: {Percent(spent * 100m / monthTotal)}",
                        $"limite sugerido (média dos últimos 3 meses): {Money(average)}"
                    ]
                };
            }
        }
    }
}
