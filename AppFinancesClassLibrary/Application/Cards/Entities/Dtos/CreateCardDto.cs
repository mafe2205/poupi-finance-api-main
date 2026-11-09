using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Cards.Entities.Dtos
{
    public record CreateCardDto
    {
        [Required] public long BankAccountId { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public CardsType Type { get; set; }
        public int LimitValueCents { get; set; }
        [Required] public int ClosingDate { get; set; }
        [Required] public int DueDate { get; set; }
    }
}
