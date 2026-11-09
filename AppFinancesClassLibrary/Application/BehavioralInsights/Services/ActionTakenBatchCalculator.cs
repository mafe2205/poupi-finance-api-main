using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Services
{
    public class ActionTakenBatchCalculator(
        IBehavioralInsightsRepository behavioralInsightsRepository,
        IBehavioralInsightsService behavioralInsightsService,
        ILogger<ActionTakenBatchCalculator> logger) : IActionTakenBatchCalculator
    {
        private const int ObservationWindowDays = 30;

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var viewedBefore = DateTime.UtcNow.AddDays(-ObservationWindowDays);
            var pendingIds = await behavioralInsightsRepository.GetPendingActionTakenEvaluationAsync(viewedBefore);

            foreach (var id in pendingIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await behavioralInsightsService.EvaluateActionTakenAsync(id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Falha ao avaliar ActionTaken em lote para o insight {InsightId}", id);
                }
            }
        }
    }
}
