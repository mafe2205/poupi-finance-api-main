namespace AppFinancesClassLibrary.Application.Documents.Entities.Results
{
    public record DocumentItemResult
    {
        public long Id { get; set; }
        public int LineNumber { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public int UnitPriceCents { get; set; }
        public int TotalCents { get; set; }
    }
}
