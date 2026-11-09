using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Domain.Entities
{
    public class BankStatement
    {
        public long Id { get; set; }
        public long CardId { get; set; }
        public string MonthReference { get; set; } = string.Empty;
        public DateOnly ClosingDate { get; set; }
        public DateOnly DueDate { get; set; }
        public DateOnly? PaymentDate { get; set; }
        public BankStatementsStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; }
    }
}
