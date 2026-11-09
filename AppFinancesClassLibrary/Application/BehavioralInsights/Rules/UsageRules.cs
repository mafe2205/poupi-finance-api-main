using AppFinancesClassLibrary.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using static AppFinancesClassLibrary.Application.BehavioralInsights.Rules.InsightText;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    public class PendingDocumentsRule : IInsightRule
    {
        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var pending = ctx.Data.PendingDocuments;
            if (pending.Count == 0) yield break;

            var oldestAgeDays = pending.Max(d => ctx.Today.DayNumber - DateOnly.FromDateTime(d.CreatedAt).DayNumber);
            if (oldestAgeDays <= ctx.Options.Usage.DocsPendentes.Dias) yield break;

            var extracted = pending.Where(d => d.ExtractedValueCents is not null).ToList();

            yield return new InsightCandidate
            {
                RuleCode = InsightRuleCodes.DocumentosPendentes,
                Type = BehavioralInsightTypes.sugestao,
                ReferenceDate = ctx.Today,
                CooldownDays = 3,
                Title = "Você tem comprovantes para revisar",
                Goal = "lembrar o usuário de revisar os comprovantes lidos que ainda não viraram transação",
                Facts =
                [
                    $"documentos pendentes de revisão: {pending.Count}",
                    $"o mais antigo está esperando há {oldestAgeDays} dias",
                    extracted.Count > 0
                        ? $"valor total aproximado lido pelo OCR: {Money(extracted.Sum(d => (long)d.ExtractedValueCents!.Value))} (em {extracted.Count} de {pending.Count} documentos)"
                        : "o OCR não conseguiu extrair o valor total dos documentos"
                ]
            };
        }
    }

    public class UncategorizedTransactionsRule : IInsightRule
    {
        private const int MinTransactions = 15;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Semanal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var transactions = ctx.Between(ctx.CurrentMonthStart, ctx.Today).Where(t => !ctx.IsGoalMovement(t)).ToList();
            if (transactions.Count < MinTransactions) yield break;

            var generic = ctx.Options.Usage.SemCategoria.EffectiveCategoriasGenericas.Select(NormalizeDescription).ToHashSet();
            var uncategorized = transactions
                .Where(t => ctx.Category(t.CategoryId) is { } c && generic.Contains(NormalizeDescription(c.Name)))
                .ToList();

            var share = (decimal)uncategorized.Count / transactions.Count;
            if (share <= ctx.Options.Usage.SemCategoria.Percentual) yield break;

            var topDescriptions = uncategorized
                .GroupBy(t => NormalizeDescription(t.Description))
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.First().Description);

            yield return new InsightCandidate
            {
                RuleCode = InsightRuleCodes.MuitasSemCategoria,
                Type = BehavioralInsightTypes.sugestao,
                ReferenceDate = ctx.LastClosedWeekEnd,
                CooldownDays = 14,
                Title = "Muitas transações sem categoria definida",
                Goal = "avisar que boa parte das transações do mês está sem uma categoria específica, o que atrapalha as análises, e sugerir categorizá-las",
                Facts =
                [
                    $"percentual das transações do mês sem categoria específica: {Percent(share * 100m)}",
                    $"quantidade: {uncategorized.Count} de {transactions.Count}",
                    $"descrições mais frequentes: {string.Join(", ", topDescriptions)}"
                ]
            };
        }
    }

    public class NoRecordsRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.UmMes;
        private const int ActiveWindowDays = 60;
        private const int MinTransactionsBeforePause = 10;
        private const int MaxRemindersPerPause = 3;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            if (ctx.Data.LastRegisteredAt is not { } lastRegisteredAt) yield break;

            var lastDate = DateOnly.FromDateTime(lastRegisteredAt);
            var idleDays = ctx.Today.DayNumber - lastDate.DayNumber;
            if (idleDays < ctx.Options.Usage.SemRegistros.Dias) yield break;

            var before = ctx.Data.Transactions.Count(t =>
                DateOnly.FromDateTime(t.CreatedAt) is var created && created <= lastDate && created > lastDate.AddDays(-ActiveWindowDays));
            if (before < MinTransactionsBeforePause) yield break;

            var remindersInThisPause = ctx.Data.History.Count(h => h.RuleCode == InsightRuleCodes.SemRegistros && h.ReferenceDate > lastDate);
            if (remindersInThisPause >= MaxRemindersPerPause) yield break;

            yield return new InsightCandidate
            {
                RuleCode = InsightRuleCodes.SemRegistros,
                Type = BehavioralInsightTypes.sugestao,
                ReferenceDate = ctx.Today,
                CooldownDays = 7,
                Title = "Que tal registrar seus gastos?",
                Goal = "lembrar o usuário de registrar as transações, porque ele está há alguns dias sem lançar nada",
                Facts =
                [
                    $"dias sem registrar transações: {idleDays}",
                    $"data do último registro: {Date(lastDate)}",
                    $"média de transações por semana antes da pausa: {Math.Round(before / (ActiveWindowDays / 7m), 1)}"
                ]
            };
        }
    }
}
