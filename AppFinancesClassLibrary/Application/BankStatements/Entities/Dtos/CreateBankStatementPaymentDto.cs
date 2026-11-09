using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.BankStatements.Entities.Dtos
{
    public record CreateBankStatementPaymentDto
    {
        public long? BankAccountId { get; set; }
        [Required] public int AmountCents { get; set; }
        [Required] public DateOnly PaymentDate { get; set; }
    }
}
