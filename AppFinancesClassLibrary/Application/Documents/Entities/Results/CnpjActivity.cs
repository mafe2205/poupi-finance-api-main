namespace AppFinancesClassLibrary.Application.Documents.Entities.Results
{
    public sealed record CnpjActivity(string Cnpj, int? Cnae, string Description, string TradeName = null, string LegalName = null)
    {
        public string RegistryName =>
            !string.IsNullOrWhiteSpace(TradeName) ? TradeName.Trim()
            : !string.IsNullOrWhiteSpace(LegalName) ? LegalName.Trim()
            : null;
    }
}
