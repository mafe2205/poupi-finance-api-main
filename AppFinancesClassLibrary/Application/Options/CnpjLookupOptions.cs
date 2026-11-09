namespace AppFinancesClassLibrary.Application.Options
{
    public class CnpjLookupOptions
    {
        public bool Enabled { get; set; } = true;
        public string BaseUrl { get; set; } = "https://brasilapi.com.br/api/cnpj/v1/";
        public int TimeoutSeconds { get; set; } = 10;
    }
}
