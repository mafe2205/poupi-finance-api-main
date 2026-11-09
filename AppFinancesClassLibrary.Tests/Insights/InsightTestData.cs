using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Application.Options;
using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Tests.Insights
{
    /// Monta a foto de dados de um usuário para os testes das regras, sem banco. Hoje é sempre
    /// quarta-feira, 14/10/2026: a última semana fechada vai de 05/10 a 11/10, a anterior de
    /// 28/09 a 04/10, e o último mês fechado é setembro.
    public class InsightTestData
    {
        public static readonly DateOnly Today = new(2026, 10, 14);
        public static readonly DateOnly LastWeekStart = new(2026, 10, 5);
        public static readonly DateOnly PreviousWeekStart = new(2026, 9, 28);
        public static readonly DateOnly MonthStart = new(2026, 10, 1);

        private long nextId = 1;

        public InsightUserData Data { get; } = new() { UserId = 1 };
        public InsightRuleOptions Options { get; } = new();

        /// Quando definido, sobrescreve a data da primeira transação (piso de histórico)
        public DateOnly? HistorySince { get; set; }

        public static DateOnly Days(int offset) => Today.AddDays(offset);

        public long Category(string name = "Mercado", CategoriesTypes type = CategoriesTypes.Saida, int limitCents = 0, bool isDefault = false)
        {
            var id = nextId++;
            Data.Categories.Add(new InsightCategory { Id = id, Name = name, Type = type, LimitValueCents = limitCents, IsDefault = isDefault });
            return id;
        }

        public InsightTransaction Tx(long categoryId, int cents, DateOnly date, string description = "compra",
            long? cardId = null, long? accountId = null, PaymentMethod method = PaymentMethod.pix,
            long? metaId = null, GoalContributionDirection? direction = null, long? statementId = null, DateTime? createdAt = null)
        {
            var transaction = new InsightTransaction
            {
                Id = nextId++,
                CategoryId = categoryId,
                PaymentValueCents = cents,
                TransactionDate = date,
                Description = description,
                CardId = cardId,
                BankAccountId = accountId,
                PaymentMethod = method,
                MetaId = metaId,
                MetaDirection = direction,
                BankStatementId = statementId,
                CreatedAt = createdAt ?? date.ToDateTime(new TimeOnly(12, 0))
            };
            Data.Transactions.Add(transaction);
            return transaction;
        }

        public InsightGoal Goal(int targetCents, DateOnly start, DateOnly end, long contributedCents = 0, DateOnly? lastDeposit = null, string name = "Viagem")
        {
            var goal = new InsightGoal
            {
                Id = nextId++,
                Description = name,
                InicialDate = start,
                FinalDate = end,
                FinalValueCents = targetCents,
                ContributedCents = contributedCents,
                LastDepositDate = lastDeposit
            };
            Data.Goals.Add(goal);
            return goal;
        }

        public InsightAccount Account(int balanceCents, string name = "Banco")
        {
            var account = new InsightAccount { Id = nextId++, BankName = name, BalanceCents = balanceCents, CreatedAt = Days(-365).ToDateTime(TimeOnly.MinValue) };
            Data.Accounts.Add(account);
            return account;
        }

        public InsightCard Card(int limitCents, long accountId = 0, string name = "Cartão", CardsType type = CardsType.credito,
            DateOnly? createdOn = null, DateOnly? lastTransaction = null)
        {
            var card = new InsightCard
            {
                Id = nextId++,
                BankAccountId = accountId,
                Name = name,
                Type = type,
                LimitValueCents = limitCents,
                ClosingDate = 1,
                DueDate = 10,
                CreatedAt = (createdOn ?? Days(-365)).ToDateTime(TimeOnly.MinValue),
                LastTransactionDate = lastTransaction
            };
            Data.Cards.Add(card);
            return card;
        }

        public InsightStatement Statement(long cardId, DateOnly closing, DateOnly due, long totalCents, int transactionCount = 1,
            BankStatementsStatus status = BankStatementsStatus.aberta, Dictionary<long, long> categoryTotals = null)
        {
            var statement = new InsightStatement
            {
                Id = nextId++,
                CardId = cardId,
                MonthReference = closing.ToString("yyyy-MM"),
                ClosingDate = closing,
                DueDate = due,
                TotalCents = totalCents,
                TransactionCount = transactionCount,
                Status = status,
                CategoryTotals = categoryTotals ?? []
            };
            Data.Statements.Add(statement);
            return statement;
        }

        public void History(string ruleCode, DateOnly referenceDate, long? relatedEntityId = null, long? categoryId = null,
            BehavioralInsightTypes type = BehavioralInsightTypes.sugestao, DateTime? createdAt = null)
        {
            Data.History.Add(new InsightHistoryEntry
            {
                RuleCode = ruleCode,
                Type = type,
                RelatedEntityId = relatedEntityId,
                CategoryId = categoryId,
                ReferenceDate = referenceDate,
                CreatedAt = createdAt ?? referenceDate.ToDateTime(TimeOnly.MinValue)
            });
        }

        public IReadOnlyList<InsightCandidate> Run<TRule>() where TRule : IInsightRule, new()
        {
            var rule = new TRule();
            Data.FirstTransactionDate = HistorySince ?? (Data.Transactions.Count > 0 ? Data.Transactions.Min(t => t.TransactionDate) : null);
            Data.LastRegisteredAt = Data.Transactions.Count > 0 ? Data.Transactions.Max(t => t.CreatedAt) : null;
            return InsightRuleEngine.Evaluate(Data, Options, rule.Frequency, Today, [rule]);
        }
    }
}
