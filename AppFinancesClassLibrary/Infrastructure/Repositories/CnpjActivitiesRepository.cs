using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class CnpjActivitiesRepository(IUnitOfWork unitOfWork) : ICnpjActivitiesRepository
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGet = "SELECT CNPJ, CNAE, DESCRIPTION, TRADE_NAME, LEGAL_NAME FROM CNPJ_ACTIVITIES WHERE CNPJ = :Cnpj";

        private const string sqlUpsert = @"
            INSERT INTO CNPJ_ACTIVITIES (CNPJ, CNAE, DESCRIPTION, TRADE_NAME, LEGAL_NAME, FETCHED_AT)
            VALUES (:Cnpj, :Cnae, :Description, :TradeName, :LegalName, :FetchedAt)
            ON CONFLICT (CNPJ) DO UPDATE SET CNAE = EXCLUDED.CNAE, DESCRIPTION = EXCLUDED.DESCRIPTION,
                TRADE_NAME = EXCLUDED.TRADE_NAME, LEGAL_NAME = EXCLUDED.LEGAL_NAME, FETCHED_AT = EXCLUDED.FETCHED_AT";
        #endregion

        public async Task<CnpjActivity> GetAsync(string cnpj)
        {
            var row = await unitOfWork.Connection.QueryFirstOrDefaultAsync<CnpjActivityRow>(sqlGet, new { Cnpj = cnpj }, unitOfWork.Transaction);
            return row is null ? null : new CnpjActivity(row.Cnpj, row.Cnae, row.Description, row.TradeName, row.LegalName);
        }

        public Task SaveAsync(CnpjActivity activity)
        {
            return unitOfWork.Connection.ExecuteAsync(sqlUpsert,
                new { activity.Cnpj, activity.Cnae, activity.Description, activity.TradeName, activity.LegalName, FetchedAt = DateTime.UtcNow }, unitOfWork.Transaction);
        }

        private sealed class CnpjActivityRow
        {
            public string Cnpj { get; set; }
            public int? Cnae { get; set; }
            public string Description { get; set; }
            public string TradeName { get; set; }
            public string LegalName { get; set; }
        }
    }
}
