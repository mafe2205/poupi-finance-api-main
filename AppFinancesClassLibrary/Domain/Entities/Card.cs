using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Domain.Entities
{
    public class Card
    {
        public long Id { get; set; }
        public long BankAccountId { get; set; }
        public string Name { get; set; }
        public CardsType Type { get; set; }
        public int LimitValueCents { get; set; }
        public int ClosingDate { get; set; }
        public int DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Active { get; set; } = true;
        public DateTime? DeletedAt { get; set; }
    }
}
