using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    public class InsightUserData
    {
        public long UserId { get; set; }
        public List<InsightCategory> Categories { get; set; } = [];
        public List<InsightTransaction> Transactions { get; set; } = [];
        public List<InsightGoal> Goals { get; set; } = [];
        public List<InsightCard> Cards { get; set; } = [];
        public List<InsightStatement> Statements { get; set; } = [];
        public List<InsightAccount> Accounts { get; set; } = [];
        public List<InsightPendingDocument> PendingDocuments { get; set; } = [];
        public List<InsightHistoryEntry> History { get; set; } = [];
        public DateOnly? FirstTransactionDate { get; set; }
        public DateTime? LastRegisteredAt { get; set; }
    }

    public class InsightTransaction
    {
        public long Id { get; set; }
        public long CategoryId { get; set; }
        public long? MetaId { get; set; }
        public GoalContributionDirection? MetaDirection { get; set; }
        public long? CardId { get; set; }
        public long? BankAccountId { get; set; }
        public long? BankStatementId { get; set; }
        public string Description { get; set; } = string.Empty;
        public PaymentMethod PaymentMethod { get; set; }
        public int PaymentParcels { get; set; } = 1;
        public int PaymentValueCents { get; set; }
        public DateOnly TransactionDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class InsightCategory
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public CategoriesTypes Type { get; set; }
        public bool IsDefault { get; set; }
        public int LimitValueCents { get; set; }
        public bool Active { get; set; } = true;
    }

    public class InsightGoal
    {
        public long Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateOnly InicialDate { get; set; }
        public DateOnly FinalDate { get; set; }
        public int InicialValueCents { get; set; }
        public int FinalValueCents { get; set; }
        public long ContributedCents { get; set; }
        public DateOnly? LastDepositDate { get; set; }
        public long SavedCents => InicialValueCents + ContributedCents;
        public long RemainingCents => Math.Max(0, FinalValueCents - SavedCents);
        public decimal ProgressPercent => FinalValueCents <= 0 ? 0 : SavedCents * 100m / FinalValueCents;
    }

    public class InsightCard
    {
        public long Id { get; set; }
        public long BankAccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public CardsType Type { get; set; }
        public int LimitValueCents { get; set; }
        public int ClosingDate { get; set; }
        public int DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Active { get; set; } = true;
        public DateOnly? LastTransactionDate { get; set; }
    }

    public class InsightStatement
    {
        public long Id { get; set; }
        public long CardId { get; set; }
        public string MonthReference { get; set; } = string.Empty;
        public DateOnly ClosingDate { get; set; }
        public DateOnly DueDate { get; set; }
        public BankStatementsStatus Status { get; set; }
        public long TotalCents { get; set; }
        public long PaidCents { get; set; }
        public long RemainingCents => Status == BankStatementsStatus.paga ? 0 : Math.Max(0, TotalCents - PaidCents);
        public int TransactionCount { get; set; }
        public Dictionary<long, long> CategoryTotals { get; set; } = [];
    }

    public class InsightAccount
    {
        public long Id { get; set; }
        public string BankName { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public int BalanceCents { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class InsightPendingDocument
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TextOcr { get; set; } = string.Empty;
        public int? ExtractedValueCents { get; set; }
    }

    public class InsightHistoryEntry
    {
        public string RuleCode { get; set; }
        public BehavioralInsightTypes Type { get; set; }
        public long? CategoryId { get; set; }
        public long? RelatedEntityId { get; set; }
        public DateOnly ReferenceDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string RelatedDescription { get; set; }
    }
}
