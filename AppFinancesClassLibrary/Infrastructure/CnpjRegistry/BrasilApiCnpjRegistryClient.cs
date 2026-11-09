using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Application.Documents.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AppFinancesClassLibrary.Infrastructure.CnpjRegistry
{
    /// BrasilAPI (https://brasilapi.com.br/docs#tag/CNPJ): dados públicos da Receita Federal. 
    public class BrasilApiCnpjRegistryClient(HttpClient httpClient) : ICnpjRegistryClient
    {
        public async Task<CnpjActivity> FetchAsync(string cnpj, CancellationToken cancellationToken = default)
        {
            using var response = await httpClient.GetAsync(cnpj, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return new CnpjActivity(cnpj, null, null);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<BrasilApiCnpjResponse>(cancellationToken: cancellationToken);
            return new CnpjActivity(cnpj, payload?.CnaeFiscal is > 0 ? payload.CnaeFiscal : null, payload?.CnaeFiscalDescricao,
                payload?.NomeFantasia, payload?.RazaoSocial);
        }

        private sealed class BrasilApiCnpjResponse
        {
            [JsonPropertyName("cnae_fiscal")] public int? CnaeFiscal { get; set; }
            [JsonPropertyName("cnae_fiscal_descricao")] public string CnaeFiscalDescricao { get; set; }
            [JsonPropertyName("nome_fantasia")] public string NomeFantasia { get; set; }
            [JsonPropertyName("razao_social")] public string RazaoSocial { get; set; }
        }
    }
}
