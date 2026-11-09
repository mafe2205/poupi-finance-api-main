using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public class CnpjActivityService(
        ICnpjActivitiesRepository cnpjActivitiesRepository,
        ICnpjRegistryClient cnpjRegistryClient,
        IOptions<CnpjLookupOptions> options,
        ILogger<CnpjActivityService> logger) : ICnpjActivityService
    {
        private readonly ICnpjActivitiesRepository cnpjActivitiesRepository = cnpjActivitiesRepository;
        private readonly ICnpjRegistryClient cnpjRegistryClient = cnpjRegistryClient;
        private readonly CnpjLookupOptions options = options.Value;
        private readonly ILogger<CnpjActivityService> logger = logger;

        public async Task<CnpjActivity> GetAsync(string cnpj, CancellationToken cancellationToken = default)
        {
            if (!options.Enabled || !DocumentParsingService.IsValidCnpj(cnpj)) return null;

            var cached = await cnpjActivitiesRepository.GetAsync(cnpj);
            if (cached is not null) return cached;

            try
            {
                var activity = await cnpjRegistryClient.FetchAsync(cnpj, cancellationToken);
                await cnpjActivitiesRepository.SaveAsync(activity);
                return activity;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                logger.LogWarning(ex, "Falha ao consultar a atividade do CNPJ {Cnpj}", cnpj);
                return null;
            }
        }
    }
}
