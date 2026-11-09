using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.BankStatements.Entities.Dtos
{
    public record CreateBankStatementInternalDto
    {
        public long CardId { get; set; }
        public string MonthReference { get; set; } = string.Empty;
        public DateOnly ClosingDate { get; set; }
        public DateOnly DueDate { get; set; }
        public BankStatementsStatus Status { get; set; }
    }
}
