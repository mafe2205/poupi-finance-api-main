using AppFinancesClassLibrary.Domain.Enums;
using static AppFinancesClassLibrary.Application.BehavioralInsights.Rules.InsightText;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    public class LowBalanceRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.DuasSemanas;

        private const int WindowDays = 30;
        private const int MinExpenseTransactions = 10;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            if (ctx.DaysSinceLastRegistration is { } idle && idle >= ctx.Options.Usage.SemRegistros.Dias) yield break;
            if (!ctx.HasHistorySince(ctx.Today.AddDays(-WindowDays))) yield break;

            var windowStart = ctx.Today.AddDays(-(WindowDays - 1));

            foreach (var account in ctx.Data.Accounts)
            {
                var outflows = ctx.Expenses(windowStart, ctx.Today).Where(t => ctx.AccountOf(t) == account.Id).ToList();
                if (outflows.Count < MinExpenseTransactions) continue;

                var dailyAverage = outflows.Sum(t => (decimal)t.PaymentValueCents) / WindowDays;
                var balance = ctx.CurrentBalance(account);
                if (dailyAverage <= 0 || balance >= dailyAverage * ctx.Options.Accounts.SaldoBaixo.DiasCobertura) continue;

                var daysCovered = balance <= 0 ? 0 : (int)Math.Floor(balance / dailyAverage);

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.SaldoBaixo,
                    Type = BehavioralInsightTypes.alerta_gasto,
                    RelatedEntityId = account.Id,
                    ReferenceDate = ctx.Today,
                    CooldownDays = 7,
                    Title = Title($"Saldo baixo em {account.BankName}"),
                    Goal = "avisar que o saldo da conta cobre poucos dias de gasto no ritmo atual",
                    Facts =
                    [
                        $"conta: {account.BankName}",
                        $"saldo: {Money(balance)}",
                        $"gasto diário médio: {Money(dailyAverage)}",
                        $"dias que o saldo cobre: {daysCovered}"
                    ]
                };
            }
        }
    }

    public class InsufficientBalanceForStatementRule : IInsightRule
    {
        public InsightRuleFrequency Frequency => InsightRuleFrequency.Diaria;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var days = ctx.Options.Accounts.FaturaVinculada.DiasAviso;

            foreach (var account in ctx.Data.Accounts)
            {
                var dueStatements = ctx.ActiveCreditCards
                    .Where(c => c.BankAccountId == account.Id)
                    .SelectMany(ctx.ClosedStatements)
                    .Where(s => s.RemainingCents > 0)
                    .Where(s => s.DueDate >= ctx.Today && s.DueDate.DayNumber - ctx.Today.DayNumber <= days)
                    .ToList();
                if (dueStatements.Count == 0) continue;

                var due = dueStatements.Sum(s => s.RemainingCents);
                var balance = ctx.CurrentBalance(account);
                if (balance >= due) continue;

                var firstDue = dueStatements.Min(s => s.DueDate);

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.SaldoInsuficienteFatura,
                    Type = BehavioralInsightTypes.alerta_gasto,
                    RelatedEntityId = account.Id,
                    ReferenceDate = firstDue,
                    Title = Title($"Saldo insuficiente para a fatura em {account.BankName}"),
                    Goal = "avisar que a conta ligada ao cartão não tem saldo para pagar a fatura que está para vencer",
                    Facts =
                    [
                        $"conta: {account.BankName}",
                        $"saldo: {Money(balance)}",
                        $"valor da fatura: {Money(due)}",
                        $"vencimento: {Date(firstDue)}",
                        $"quanto falta: {Money(due - balance)}"
                    ]
                };
            }
        }
    }
    public class IdleBalanceRule : IInsightRule
    {
        public InsightHistoryTier HistoryTier => InsightHistoryTier.UmMes;

        private const int ExpenseMonths = 3;

        public InsightRuleFrequency Frequency => InsightRuleFrequency.Mensal;

        public IEnumerable<InsightCandidate> Evaluate(InsightRuleContext ctx)
        {
            var options = ctx.Options.Accounts.SaldoParado;
            if (!ctx.HasClosedMonths(ExpenseMonths)) yield break;

            var monthlyExpenses = ctx.AverageMonthlyExpenses(ExpenseMonths);
            if (monthlyExpenses <= 0) yield break;

            var windowStart = ctx.Today.AddDays(-options.DiasParado);
            var contributedToGoal = ctx.Between(windowStart, ctx.Today)
                .Any(t => t.MetaId is not null && t.MetaDirection == GoalContributionDirection.deposito);
            if (contributedToGoal) yield break;

            var threshold = monthlyExpenses * options.Multiplo;
            var checkpoints = Enumerable.Range(0, options.DiasParado / 7 + 1).Select(week => ctx.Today.AddDays(-7 * week)).ToList();

            foreach (var account in ctx.Data.Accounts)
            {
                if (checkpoints.Any(date => ctx.BalanceAt(account, date) <= threshold)) continue;

                var balance = ctx.CurrentBalance(account);

                yield return new InsightCandidate
                {
                    RuleCode = InsightRuleCodes.SaldoParado,
                    Type = BehavioralInsightTypes.sugestao,
                    RelatedEntityId = account.Id,
                    ReferenceDate = ctx.LastClosedMonthStart,
                    CooldownDays = 90,
                    Title = Title($"Dinheiro parado em {account.BankName}"),
                    Goal = "sugerir criar uma meta ou uma reserva de emergência com o dinheiro que está parado na conta há semanas",
                    Facts =
                    [
                        $"conta: {account.BankName}",
                        $"saldo: {Money(balance)}",
                        $"gasto mensal médio: {Money(monthlyExpenses)}",
                        $"meses de gasto que o saldo cobre: {Math.Round(balance / monthlyExpenses, 1)}"
                    ]
                };
            }
        }
    }
}
