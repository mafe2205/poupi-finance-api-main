using AppFinancesClassLibrary.Application.Documents.Entities.Results;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public interface ICnpjRegistryClient
    {
        Task<CnpjActivity> FetchAsync(string cnpj, CancellationToken cancellationToken = default);
    }
}
