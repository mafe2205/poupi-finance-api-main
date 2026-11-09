namespace AppFinancesClassLibrary.Domain.Entities
{
    public class DocumentItem
    {
        public long Id { get; set; }
        public long DocumentId { get; set; }
        public int LineNumber { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public int UnitPriceCents { get; set; }
        public int TotalCents { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
