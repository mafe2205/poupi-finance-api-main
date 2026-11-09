using AppFinancesClassLibrary.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Cards.Entities.Dtos
{
    public record UpdateCardDto
    {
        [Required] public long Id { get; set; }
        public string Name { get; set; }
        public CardsType Type { get; set; }
        public int LimitValueCents { get; set; }
        public int ClosingDate { get; set; }
        public int DueDate { get; set; }
    }
}
