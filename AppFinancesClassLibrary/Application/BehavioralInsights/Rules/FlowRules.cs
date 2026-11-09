using AppFinancesClassLibrary.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using static AppFinancesClassLibrary.Application.BehavioralInsights.Rules.InsightText;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    public class SpendingAboveIncomeRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DuasSemanas;

        private const int IncomeLookbackDays = 60;
        private const int MinDaysIntoMonth = 15;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Semanal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            if (!ctx.Incomes(ctx.Today.AddDays(-IncomeLookbackDays), ctx.Today).Any()) yield break;

            var weeks = Math.Max(1, ctx.Options.Flow.DeficitSemanas);
            var weekStarts = Enumerable.Range(0, weeks).Select(i => ctx.LastClosedWeekStart.AddDays(-7 * i)).ToList();
            var everyWeekInDeficit = weekStarts.All(start => ctx.SumExpenses(start, start.AddDays(6)) > ctx.SumIncomes(start, start.AddDays(6)));

            DateOnly from, to;
            string period;
            if (everyWeekInDeficit)
            {
                (from, to) = (weekStarts[^1], ctx.LastClosedWeekEnd);
                period = $"últimas {weeks} semanas ({Date(from)} a {Date(to)})";
            }
            else if (ctx.Today.Day >= MinDaysIntoMonth && ctx.SumExpenses(ctx.CurrentMonthStart, ctx.Today) > ctx.SumIncomes(ctx.CurrentMonthStart, ctx.Today))
            {
                (from, to) = (ctx.CurrentMonthStart, ctx.Today);
                period = $"mês atual até hoje ({Date(from)} a {Date(to)})";
            }
            else
            {
                yield break;
            }

            var income = ctx.SumIncomes(from, to);
            var expenses = ctx.SumExpenses(from, to);
            var topCategory = ctx.ExpensesByCategory(from, to).MaxBy(c => c.Value);

            yield return new InsightCandidate
            {
                RuleCode = InsightRuleCodes.GastosMaioresReceitas,
                Type = BehavioralInsightTypes.alerta_gasto,
                ReferenceDate = ctx.LastClosedWeekEnd,
                CooldownDays = 14,
                Title = "Você está gastando mais do que ganha",
                Goal = "avisar que o usuário está gastando mais do que recebe no período",
                Facts =
                [
                    $"período: {period}",
                    $"entradas: {Money(income)}",
                    $"saídas: {Money(expenses)}",
                    $"déficit: {Money(expenses - income)}",
                    $"categoria com maior gasto: {ctx.Category(topCategory.Key)?.Name ?? "sem categoria"} ({Money(topCategory.Value)})"
                ]
            };
        }
    }

    public class LowSavingsRateRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.UmMes;

        private const int MinTransactions = 10;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Mensal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var monthStart = ctx.LastClosedMonthStart;
            var monthEnd = InsightRuleContext.MonthEnd(monthStart);
            if (ctx.Between(monthStart, monthEnd).Count(t => !ctx.IsGoalMovement(t)) < MinTransactions) yield break;

            var income = ctx.SumIncomes(monthStart, monthEnd);
            var expenses = ctx.SumExpenses(monthStart, monthEnd);

            if (income <= 0 || expenses > income) yield break;

            var minimumRate = ctx.Options.Flow.Poupanca.TaxaMinima;
            var rate = (decimal)(income - expenses) / income;
            if (rate >= minimumRate) yield break;

            yield return new InsightCandidate
            {
                RuleCode = InsightRuleCodes.PoupancaBaixa,
                Type = BehavioralInsightTypes.sugestao,
                ReferenceDate = monthStart,
                Title = "Sobrou pouco no mês passado",
                Goal = "avisar que sobrou pouco da renda no mês passado e sugerir guardar mais",
                Facts =
                [
                    $"entradas do mês: {Money(income)}",
                    $"saídas do mês: {Money(expenses)}",
                    $"taxa de poupança: {Percent(rate * 100m)}",
                    $"quanto faltou para poupar {Percent(minimumRate * 100m)}: {Money(minimumRate * income - (income - expenses))}"
                ]
            };
        }
    }

    public class IncomeDropRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DoisMeses;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Mensal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            if (!ctx.HasClosedMonths(4)) yield break;

            long IncomeOf(int monthsAgo) => ctx.SumIncomes(ctx.ClosedMonthStart(monthsAgo), InsightRuleContext.MonthEnd(ctx.ClosedMonthStart(monthsAgo)));

            var previous = new[] { IncomeOf(2), IncomeOf(3), IncomeOf(4) };
            if (previous.Any(i => i <= 0)) yield break;

            var income = IncomeOf(1);
            var average = (decimal)previous.Average();
            if (income > average * (1m - ctx.Options.Flow.QuedaReceita.Percentual)) yield break;

            yield return new InsightCandidate
            {
                RuleCode = InsightRuleCodes.QuedaReceita,
                Type = BehavioralInsightTypes.padrao_detectado,
                ReferenceDate = ctx.LastClosedMonthStart,
                Title = "Suas entradas caíram",
                Goal = "comentar que as entradas do mês passado ficaram bem abaixo da média recente",
                Facts =
                [
                    $"entradas do mês passado: {Money(income)}",
                    $"média dos 3 meses anteriores: {Money(average)}",
                    $"diferença: {Money(average - income)} ({Percent((average - income) * 100m / average)})"
                ]
            };
        }
    }

    public class OutlierExpenseRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DuasSemanas;

        private const int RecentDays = 7;
        private const int HistoryDays = 90;
        private const int MinHistoryTransactions = 10;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var options = ctx.Options.Flow.ForaPadrao;
            var recent = ctx.Expenses(ctx.Today.AddDays(-(RecentDays - 1)), ctx.Today)
                .Where(t => t.PaymentValueCents >= options.ValorMinimoCentavos);

            foreach (var transaction in recent)
            {
                var history = ctx.Expenses(transaction.TransactionDate.AddDays(-HistoryDays), transaction.TransactionDate.AddDays(-1))
                    .Where(t => t.CategoryId == transaction.CategoryId)
                    .Select(t => (decimal)t.PaymentValueCents)
                    .ToList();
                if (history.Count < MinHistoryTransactions) continue;

                var mean = history.Average();
                var stdDev = (decimal)Math.Sqrt((double)history.Average(v => (v - mean) * (v - mean)));
                if (transaction.PaymentValueCents <= mean + options.K * stdDev) continue;

                var category = ctx.Category(transaction.CategoryId);

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.GastoForaPadrao,
                    Type = BehavioralInsightTypes.alerta_gasto,
                    CategoryId = transaction.CategoryId,
                    RelatedEntityId = transaction.Id,
                    ReferenceDate = ctx.Today,
                    Once = true,
                    Title = Title($"Gasto fora do padrão em {category?.Name}"),
                    Goal = "destacar uma compra muito acima do que o usuário costuma gastar nessa categoria",
                    Facts =
                    [
                        $"categoria: {category?.Name}",
                        $"descrição da compra: {transaction.Description}",
                        $"valor da compra: {Money(transaction.PaymentValueCents)}",
                        $"média da categoria nos últimos 90 dias: {Money(mean)}",
                        $"vezes acima da média: {Math.Round(transaction.PaymentValueCents / mean, 1)}"
                    ]
                };
            }
        }
    }

    public class RecurringExpenseRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DoisMeses;

        private const int LookbackDays = 180;
        private const int MinGapDays = 25;
        private const int MaxGapDays = 35;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Semanal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var options = ctx.Options.Flow.Recorrente;

            var groups = ctx.Expenses(ctx.Today.AddDays(-LookbackDays), ctx.Today)
                .Where(t => t.PaymentParcels <= 1)
                .GroupBy(t => NormalizeDescription(t.Description))
                .Where(g => g.Key.Length > 0);

            foreach (var group in groups)
            {
                var chain = FindLatestChain(group.OrderBy(t => t.TransactionDate).ToList(), options.Tolerancia);
                if (chain.Count < options.Ocorrencias) continue;

                var last = chain[^1];
                if (ctx.Today.DayNumber - last.TransactionDate.DayNumber > MaxGapDays) continue;

                var average = chain.Average(t => (decimal)t.PaymentValueCents);
                var category = ctx.Category(last.CategoryId);

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.GastoRecorrente,
                    Type = BehavioralInsightTypes.padrao_detectado,
                    CategoryId = last.CategoryId,
                    RelatedEntityId = last.Id,
                    Subject = group.Key,
                    ReferenceDate = ctx.LastClosedWeekEnd,
                    CooldownDays = 90,
                    Title = Title($"Gasto recorrente: {last.Description}"),
                    Goal = "mostrar quanto uma cobrança mensal recorrente custa por ano e sugerir revisar se a assinatura ainda é usada",
                    Facts =
                    [
                        $"descrição: {last.Description}",
                        $"categoria: {category?.Name}",
                        $"valor médio por mês: {Money(average)}",
                        $"meses seguidos identificados: {chain.Count}",
                        $"total anual estimado: {Money(average * 12)}"
                    ]
                };
            }
        }

        private static List<InsightTransaction> FindLatestChain(List<InsightTransaction> ordered, decimal tolerance)
        {
            var chain = new List<InsightTransaction> { ordered[^1] };
            var reference = (decimal)ordered[^1].PaymentValueCents;

            for (var current = ordered[^1]; ;)
            {
                var previous = ordered
                    .Where(t => t.TransactionDate < current.TransactionDate)
                    .Where(t => current.TransactionDate.DayNumber - t.TransactionDate.DayNumber is >= MinGapDays and <= MaxGapDays)
                    .Where(t => Math.Abs(t.PaymentValueCents - reference) <= reference * tolerance)
                    .OrderBy(t => Math.Abs(current.TransactionDate.DayNumber - t.TransactionDate.DayNumber - 30))
                    .FirstOrDefault();
                if (previous is null) break;

                chain.Insert(0, previous);
                current = previous;
            }

            return chain;
        }
    }

    public class SpendingConcentrationRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.UmMes;

        private const int MinMonthExpensesCents = 50000;
        private const int MinCategories = 3;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Mensal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var options = ctx.Options.Flow.Concentracao;
            var monthStart = ctx.LastClosedMonthStart;
            var totals = ctx.ExpensesByCategory(monthStart, InsightRuleContext.MonthEnd(monthStart));
            var monthTotal = totals.Values.Sum();
            if (monthTotal <= MinMonthExpensesCents || totals.Count(t => t.Value > 0) < MinCategories) yield break;

            var excluded = (options.CategoriasExcluidas ?? []).Select(NormalizeDescription).ToHashSet();

            foreach (var (categoryId, spent) in totals.OrderByDescending(t => t.Value))
            {
                var share = (decimal)spent / monthTotal;
                if (share <= options.Percentual) break;

                var category = ctx.Category(categoryId);
                if (category is null || excluded.Contains(NormalizeDescription(category.Name))) continue;

                var others = totals
                    .Where(t => t.Key != categoryId)
                    .OrderByDescending(t => t.Value)
                    .Take(2)
                    .Select(t => $"{ctx.Category(t.Key)?.Name} ({Money(t.Value)})");

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.ConcentracaoGasto,
                    Type = BehavioralInsightTypes.padrao_detectado,
                    CategoryId = categoryId,
                    ReferenceDate = monthStart,
                    Title = Title($"{category.Name} concentra seus gastos"),
                    Goal = "comentar que uma única categoria consumiu mais da metade das saídas do mês passado",
                    Facts =
                    [
                        $"categoria: {category.Name}",
                        $"percentual das saídas do mês: {Percent(share * 100m)}",
                        $"valor: {Money(spent)}",
                        $"outras maiores categorias: {string.Join(", ", others)}"
                    ]
                };
            }
        }
    }

    public class CriticalWeekdayRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.UmMes;

        private const int MinExpenseTransactions = 30;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Mensal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var options = ctx.Options.Flow.DiaCritico;
            var months = Math.Max(1, options.MesesAnalisados);
            if (!ctx.HasClosedMonths(months)) yield break;

            var from = ctx.ClosedMonthStart(months);
            var to = InsightRuleContext.MonthEnd(ctx.LastClosedMonthStart);
            var expenses = ctx.Expenses(from, to).ToList();
            if (expenses.Count < MinExpenseTransactions) yield break;

            var total = expenses.Sum(t => (decimal)t.PaymentValueCents);
            var byDay = expenses.GroupBy(t => t.TransactionDate.DayOfWeek).ToDictionary(g => g.Key, g => g.Sum(t => (decimal)t.PaymentValueCents));
            var (day, spent) = byDay.MaxBy(d => d.Value);

            var share = spent / total;
            if (share <= options.Percentual) yield break;

            var dates = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(from.AddDays).ToList();
            var dayCount = dates.Count(d => d.DayOfWeek == day);
            var averageOnDay = spent / dayCount;
            var averageOtherDays = (total - spent) / (dates.Count - dayCount);

            yield return new InsightCandidate
            {
                RuleCode = InsightRuleCodes.DiaSemanaCritico,
                Type = BehavioralInsightTypes.padrao_detectado,
                ReferenceDate = ctx.LastClosedMonthStart,
                CooldownDays = 60,
                Title = Title($"Seus gastos se concentram na {Weekday(day)}"),
                Goal = "revelar que o gasto do usuário se concentra em um dia da semana",
                Facts =
                [
                    $"dia da semana: {Weekday(day)}",
                    $"percentual das saídas nos últimos {months} meses: {Percent(share * 100m)}",
                    $"gasto médio nesse dia: {Money(averageOnDay)}",
                    $"gasto médio nos outros dias: {Money(averageOtherDays)}"
                ]
            };
        }
    }

    public class SpendingDropRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DuasSemanas;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Semanal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var current = ctx.ExpensesByCategory(ctx.LastClosedWeekStart, ctx.LastClosedWeekEnd);
            var previous = ctx.ExpensesByCategory(ctx.PreviousWeekStart, ctx.PreviousWeekEnd);

            foreach (var category in ctx.SpendingCategories())
            {
                var previousCents = previous.GetValueOrDefault(category.Id);
                var currentCents = current.GetValueOrDefault(category.Id);
                if (previousCents < ctx.Options.MinCategorySpendCentsToConsider) continue;
                if (currentCents > previousCents * (1m - ctx.Options.Flow.QuedaGasto.Percentual)) continue;

                var recentlyAlerted = ctx.Data.History.Any(h =>
                    h.Type == BehavioralInsightTypes.alerta_gasto && h.CategoryId == category.Id &&
                    DateOnly.FromDateTime(h.CreatedAt) >= ctx.PreviousWeekStart);
                if (recentlyAlerted) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.QuedaGasto,
                    Type = BehavioralInsightTypes.sugestao,
                    CategoryId = category.Id,
                    ReferenceDate = ctx.LastClosedWeekEnd,
                    Title = Title($"Você gastou menos com {category.Name}"),
                    Goal = "parabenizar o usuário por ter reduzido o gasto na categoria em relação à semana anterior",
                    Facts =
                    [
                        $"categoria: {category.Name}",
                        $"gasto na semana anterior: {Money(previousCents)}",
                        $"gasto na última semana: {Money(currentCents)}",
                        $"economia: {Money(previousCents - currentCents)}"
                    ]
                };
            }
        }
    }
}
