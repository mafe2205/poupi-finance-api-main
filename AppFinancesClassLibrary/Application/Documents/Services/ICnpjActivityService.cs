using AppFinancesClassLibrary.Application.Documents.Entities.Results;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public interface ICnpjActivityService
    {
        Task<CnpjActivity> GetAsync(string cnpj, CancellationToken cancellationToken = default);
    }
}
