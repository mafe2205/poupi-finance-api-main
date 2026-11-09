using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.BankStatements.Entities.Dtos
{
    public record CreateBankStatementDto
    {
        [Required] public long CardId { get; set; }
        [Required] public string MonthReference { get; set; }
        [Required] public DateOnly ClosingDate { get; set; }
        [Required] public DateOnly DueDate { get; set; }
        [Required] public BankStatementsStatus Status { get; set; }
    }
}
