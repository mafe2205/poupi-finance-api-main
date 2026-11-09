namespace AppFinancesClassLibrary.Application.Categories.Classification
{
    public sealed class CategoryEvidence
    {
        public string Establishment { get; init; }
        public string EstablishmentKey { get; init; }
        public int? Cnae { get; init; }
        public string FullText { get; init; }
        public IReadOnlyList<string> ItemDescriptions { get; init; } = [];
    }
}
