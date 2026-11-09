using AppFinancesClassLibrary.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using static AppFinancesClassLibrary.Application.BehavioralInsights.Rules.InsightText;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    public class StatementNearLimitRule : IInsightRule
    {
        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            foreach (var card in ctx.ActiveCreditCards.Where(c => c.LimitValueCents > 0))
            {
                var open = ctx.OpenStatement(card);
                if (open is null || open.TotalCents <= 0) continue;

                var percent = open.TotalCents * 100m / card.LimitValueCents;

                var level = ctx.Options.Cards.Limite.EffectiveNiveis.Where(l => percent >= l).DefaultIfEmpty(0).Max();
                if (level == 0) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.FaturaLimitePrefix + level,
                    Type = BehavioralInsightTypes.alerta_gasto,
                    RelatedEntityId = card.Id,
                    ReferenceDate = open.ClosingDate,
                    Title = Title($"Fatura do {card.Name} em {level}% do limite"),
                    Goal = "avisar que a fatura aberta do cartão já consumiu boa parte do limite",
                    Facts =
                    [
                        $"cartão: {card.Name}",
                        $"fatura aberta: {Money(open.TotalCents)}",
                        $"limite: {Money(card.LimitValueCents)}",
                        $"percentual usado: {Percent(percent)}",
                        $"limite disponível: {Money(Math.Max(0, card.LimitValueCents - open.TotalCents))}"
                    ]
                };
            }
        }
    }

    public class StatementDueSoonRule : IInsightRule
    {
        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            foreach (var card in ctx.ActiveCreditCards)
            {
                foreach (var statement in ctx.ClosedStatements(card).Where(s => s.RemainingCents > 0))
                {
                    var daysToDue = statement.DueDate.DayNumber - ctx.Today.DayNumber;

                    if (daysToDue >= 0 && daysToDue <= ctx.Options.Cards.Vencimento.DiasAviso)
                    {
                        yield return new InsightCandidate
                        {
                            RuleCode = InsightRuleCodes.VencimentoProximo,
                            Type = BehavioralInsightTypes.alerta_gasto,
                            RelatedEntityId = card.Id,
                            ReferenceDate = statement.DueDate,
                            Title = Title($"Fatura do {card.Name} vence em breve"),
                            Goal = "lembrar o usuário de pagar a fatura do cartão que está para vencer",
                            Facts =
                            [
                                $"cartão: {card.Name}",
                                $"valor da fatura: {Money(statement.TotalCents)}",
                                .. StatementPaymentFacts(statement),
                                $"vencimento: {Date(statement.DueDate)}",
                                $"dias restantes: {daysToDue}"
                            ]
                        };
                    }
                    else if (daysToDue < 0 && -daysToDue <= ctx.Options.Cards.Vencimento.DiasVencidaAviso)
                    {
                        yield return new InsightCandidate
                        {
                            RuleCode = InsightRuleCodes.FaturaVencida,
                            Type = BehavioralInsightTypes.alerta_gasto,
                            RelatedEntityId = card.Id,
                            ReferenceDate = statement.DueDate,
                            Title = Title($"Fatura do {card.Name} vencida"),
                            Goal = "avisar que a fatura do cartão venceu e ainda não foi paga por completo",
                            Facts =
                            [
                                $"cartão: {card.Name}",
                                $"valor da fatura: {Money(statement.TotalCents)}",
                                .. StatementPaymentFacts(statement),
                                $"vencimento: {Date(statement.DueDate)}",
                                $"dias de atraso: {-daysToDue}"
                            ]
                        };
                    }
                }
            }
        }
    }

    public class StatementGrowthRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DoisMeses;
        public InsightRuleFrequency Frequency => InsightRuleFrequency.Mensal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var options = ctx.Options.Cards.Comparacao;

            foreach (var card in ctx.ActiveCreditCards)
            {
                var closed = ctx.ClosedStatements(card).Take(2).ToList();
                if (closed.Count < 2) continue;

                var (latest, previous) = (closed[0], closed[1]);
                if (previous.TotalCents <= options.FaturaAnteriorMinimaCentavos) continue;
                if (latest.TotalCents < previous.TotalCents * (1m + options.Aumento)) continue;

                var topCategories = latest.CategoryTotals
                    .Select(c => (CategoryId: c.Key, Growth: c.Value - previous.CategoryTotals.GetValueOrDefault(c.Key)))
                    .Where(c => c.Growth > 0)
                    .OrderByDescending(c => c.Growth)
                    .Take(3)
                    .Select(c => $"{ctx.Category(c.CategoryId)?.Name ?? "sem categoria"} (+{Money(c.Growth)})");

                var difference = latest.TotalCents - previous.TotalCents;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.FaturaMaior,
                    Type = BehavioralInsightTypes.sugestao,
                    RelatedEntityId = card.Id,
                    ReferenceDate = latest.ClosingDate,
                    Title = Title($"Fatura do {card.Name} subiu"),
                    Goal = "avisar que a fatura fechada do cartão cresceu bastante em relação à anterior e mostrar o que mais subiu",
                    Facts =
                    [
                        $"cartão: {card.Name}",
                        $"fatura fechada: {Money(latest.TotalCents)}",
                        $"fatura anterior: {Money(previous.TotalCents)}",
                        $"diferença: {Money(difference)} ({Percent(difference * 100m / previous.TotalCents)})",
                        $"categorias que mais cresceram: {string.Join(", ", topCategories)}"
                    ]
                };
            }
        }
    }

    public class StatementProjectionRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DuasSemanas;
        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var options = ctx.Options.Cards.Projecao;
            var firstAlertLevel = ctx.Options.Cards.Limite.EffectiveNiveis.Min();

            foreach (var card in ctx.ActiveCreditCards.Where(c => c.LimitValueCents > 0))
            {
                var open = ctx.OpenStatement(card);
                if (open is null || open.TransactionCount < options.TransacoesMinimas) continue;

                if (open.TotalCents * 100m / card.LimitValueCents >= firstAlertLevel) continue;

                var cycleStart = InsightRuleContext.CycleStart(open);
                var elapsedDays = ctx.Today.DayNumber - cycleStart.DayNumber + 1;
                var cycleDays = open.ClosingDate.DayNumber - cycleStart.DayNumber + 1;
                if (elapsedDays < options.DiasMinimosNoCiclo || cycleDays <= 0) continue;

                var projected = (decimal)open.TotalCents / elapsedDays * cycleDays;
                if (projected <= card.LimitValueCents) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.ProjecaoFatura,
                    Type = BehavioralInsightTypes.alerta_gasto,
                    RelatedEntityId = card.Id,
                    ReferenceDate = open.ClosingDate,
                    Title = Title($"Fatura do {card.Name} pode passar do limite"),
                    Goal = "avisar que, no ritmo atual, a fatura do cartão deve passar do limite até o fechamento",
                    Facts =
                    [
                        $"cartão: {card.Name}",
                        $"fatura aberta: {Money(open.TotalCents)}",
                        $"projeção até o fechamento: {Money(projected)}",
                        $"limite: {Money(card.LimitValueCents)}",
                        $"dias restantes no ciclo: {open.ClosingDate.DayNumber - ctx.Today.DayNumber}"
                    ]
                };
            }
        }
    }

    public class CardConcentrationRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.UmMes;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Mensal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var options = ctx.Options.Cards.Concentracao;
            var cards = ctx.ActiveCreditCards.ToDictionary(c => c.Id);
            if (cards.Count < 2) yield break;

            var monthStart = ctx.LastClosedMonthStart;
            var cardExpenses = ctx.Expenses(monthStart, InsightRuleContext.MonthEnd(monthStart))
                .Where(t => t.CardId is { } id && cards.ContainsKey(id))
                .ToList();
            if (cardExpenses.Count < options.TransacoesMinimas) yield break;

            var byCard = cards.Keys.ToDictionary(id => id, id => cardExpenses.Where(t => t.CardId == id).Sum(t => (long)t.PaymentValueCents));
            var total = byCard.Values.Sum();
            if (total <= 0) yield break;

            var (topCardId, topSpent) = byCard.MaxBy(c => c.Value);
            var share = (decimal)topSpent / total;
            if (share <= options.Percentual) yield break;

            var spare = cards.Values
                .Where(c => c.Id != topCardId && c.LimitValueCents > 0)
                .Select(c => (Card: c, Usage: (decimal)byCard[c.Id] / c.LimitValueCents))
                .Where(c => c.Usage < options.UsoMaximoOutroCartao)
                .OrderBy(c => c.Usage)
                .FirstOrDefault();
            if (spare.Card is null) yield break;

            var topCard = cards[topCardId];

            yield return new InsightCandidate
            {
                RuleCode = InsightRuleCodes.ConcentracaoCartao,
                Type = BehavioralInsightTypes.sugestao,
                RelatedEntityId = topCardId,
                ReferenceDate = monthStart,
                Title = Title($"Gastos concentrados no {topCard.Name}"),
                Goal = "sugerir distribuir os gastos entre os cartões, porque um concentra quase tudo e outro está folgado",
                Facts =
                [
                    $"cartão concentrado: {topCard.Name}",
                    $"percentual do gasto em cartões no mês passado: {Percent(share * 100m)}",
                    $"cartão com folga: {spare.Card.Name}",
                    $"limite disponível no cartão com folga: {Money(spare.Card.LimitValueCents - byCard[spare.Card.Id])}"
                ]
            };
        }
    }

    public class UnusedCardRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.UmMes;
        public InsightRuleFrequency Frequency => InsightRuleFrequency.Mensal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var days = ctx.Options.Cards.SemUso.Dias;

            foreach (var card in ctx.Data.Cards.Where(c => c.Active))
            {
                var createdOn = DateOnly.FromDateTime(card.CreatedAt);
                if (ctx.Today.DayNumber - createdOn.DayNumber <= days) continue;

                var lastUse = card.LastTransactionDate ?? createdOn;
                var daysUnused = ctx.Today.DayNumber - lastUse.DayNumber;
                if (daysUnused < days) continue;

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.CartaoSemUso,
                    Type = BehavioralInsightTypes.sugestao,
                    RelatedEntityId = card.Id,
                    ReferenceDate = ctx.LastClosedMonthStart,
                    CooldownDays = 90,
                    Title = Title($"{card.Name} sem uso"),
                    Goal = "sugerir revisar ou arquivar um cartão que o usuário parou de usar",
                    Facts =
                    [
                        $"cartão: {card.Name}",
                        $"dias sem uso: {daysUnused}",
                        card.LastTransactionDate is { } last
                            ? $"data da última transação: {Date(last)}"
                            : "o cartão nunca foi usado"
                    ]
                };
            }
        }
    }
}
