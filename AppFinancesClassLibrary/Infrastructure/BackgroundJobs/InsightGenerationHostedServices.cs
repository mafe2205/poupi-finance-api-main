using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Application.BehavioralInsights.Services;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppFinancesClassLibrary.Infrastructure.BackgroundJobs
{
    public abstract class InsightGenerationHostedServiceBase(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        InsightRuleFrequency frequency) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            do
            {
                await RunOnceAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var userIds = await userRepository.GetAllActiveIdsAsync();
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                foreach (var userId in userIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var userScope = scopeFactory.CreateScope();
                        var insightGenerationService = userScope.ServiceProvider.GetRequiredService<IInsightGenerationService>();
                        await insightGenerationService.GenerateAsync(userId, frequency, today, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Falha ao gerar insights ({Frequency}) para o usuário {UserId}", frequency, userId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // shutdown normal do host
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao rodar o job de geração de insights ({Frequency}).", frequency);
            }
        }
    }

    public class DailyInsightGenerationHostedService(IServiceScopeFactory scopeFactory, ILogger<DailyInsightGenerationHostedService> logger)
        : InsightGenerationHostedServiceBase(scopeFactory, logger, InsightRuleFrequency.Diaria);

    public class WeeklyInsightGenerationHostedService(IServiceScopeFactory scopeFactory, ILogger<WeeklyInsightGenerationHostedService> logger)
        : InsightGenerationHostedServiceBase(scopeFactory, logger, InsightRuleFrequency.Semanal);

    public class MonthlyInsightGenerationHostedService(IServiceScopeFactory scopeFactory, ILogger<MonthlyInsightGenerationHostedService> logger)
        : InsightGenerationHostedServiceBase(scopeFactory, logger, InsightRuleFrequency.Mensal);
}
