using AppFinancesClassLibrary.Application.Documents.Entities.Results;

namespace AppFinancesClassLibrary.Application.Interfaces.Repositories
{
    public interface ICnpjActivitiesRepository
    {
        Task<CnpjActivity> GetAsync(string cnpj);
        Task SaveAsync(CnpjActivity activity);
    }
}
