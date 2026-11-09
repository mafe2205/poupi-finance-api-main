using AppFinancesClassLibrary.Application.BehavioralInsights.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppFinancesClassLibrary.Infrastructure.BackgroundJobs
{
    public class ActionTakenEvaluationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ActionTakenEvaluationHostedService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var calculator = scope.ServiceProvider.GetRequiredService<IActionTakenBatchCalculator>();
                    await calculator.RunAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // shutdown normal do host
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Falha ao rodar o job de avaliação de ActionTaken.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
