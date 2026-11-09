namespace AppFinancesClassLibrary.Domain.Entities
{
    public class Goal
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public DateOnly InicialDate { get; set; }
        public DateOnly FinalDate { get; set; }
        public int InicialValueCents { get; set; }
        public int FinalValueCents { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Active { get; set; } = true;
        public DateTime? DeletedAt { get; set; }
    }
}
