using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.BankStatements.Entities.Dtos
{
    public record UpdateBankStatementDto
    {
        [Required] public long Id { get; set; }
        [Required] public DateOnly PaymentDate { get; set; }
    }
}
