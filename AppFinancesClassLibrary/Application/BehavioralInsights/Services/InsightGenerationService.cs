using AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Dtos;
using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Application.Notifications.Entities.Dtos;
using AppFinancesClassLibrary.Application.Notifications.Services;
using AppFinancesClassLibrary.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Services
{
    public class InsightGenerationService(
        IInsightTriggerService triggerService,
        IOllamaClient ollamaClient,
        IBehavioralInsightsService behavioralInsightsService,
        INotificationDispatcher notificationDispatcher,
        ILogger<InsightGenerationService> logger) : IInsightGenerationService
    {
        private const int MaxDescriptionLength = 500;

        public async Task GenerateAsync(long userId, InsightRuleFrequency frequency, DateOnly today, CancellationToken cancellationToken = default,
            IReadOnlySet<string> silentKeys = null)
        {
            var candidates = await triggerService.DetectAsync(userId, frequency, today);

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string description;
                try
                {
                    description = await ollamaClient.GenerateInsightTextAsync(BuildPrompt(candidate), cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Falha ao gerar texto via Ollama para usuário {UserId}, regra {RuleCode}", userId, candidate.RuleCode);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(description)) continue;

                description = description.Length > MaxDescriptionLength ? description[..MaxDescriptionLength] : description;

                long insightId;
                try
                {
                    insightId = await behavioralInsightsService.CreateAsync(new CreateBehavioralInsightDto
                    {
                        UserId = userId,
                        CategoryId = candidate.CategoryId,
                        Type = candidate.Type,
                        RuleCode = candidate.RuleCode,
                        RelatedEntityId = candidate.RelatedEntityId,
                        Facts = candidate.FactsText,
                        Description = description,
                        ReferenceDate = candidate.ReferenceDate,
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Falha ao salvar insight da regra {RuleCode} para o usuário {UserId}", candidate.RuleCode, userId);
                    continue;
                }

                if (silentKeys?.Contains(candidate.Key) == true) continue;

                try
                {
                    await notificationDispatcher.DispatchAsync(new CreateNotificationDto
                    {
                        UserId = userId,
                        Type = NotificationType.insight_generated,
                        Title = candidate.Title,
                        Message = description,
                        RelatedEntityId = insightId
                    }, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Falha ao criar notificação do insight {InsightId} para o usuário {UserId}", insightId, userId);
                }
            }
        }

        public static string BuildPrompt(InsightCandidate candidate)
        {
            return "Escreva, em português, uma única frase curta e natural para um app de finanças pessoais, " +
                $"com o objetivo de {candidate.Goal}. " +
                $"Use apenas estes dados, sem inventar outros números: {string.Join("; ", candidate.Facts)}. " +
                "Responda somente com a frase, sem explicações adicionais.";
        }
    }
}
