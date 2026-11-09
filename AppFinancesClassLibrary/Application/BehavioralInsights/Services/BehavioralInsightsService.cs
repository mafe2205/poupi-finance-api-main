using AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Dtos;
using AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Results;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Transactions.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;
using Microsoft.Extensions.Logging;

namespace AppFinancesClassLibrary.Application.BehavioralInsights.Services
{
    public class BehavioralInsightsService(IBehavioralInsightsRepository behavioralInsightsRepository, ITransactionsRepository transactionsRepository, ILogger<BehavioralInsightsService> logger, ICurrentUser currentUser) : IBehavioralInsightsService
    {
        private const decimal MinimumDropThreshold = 0.15m;

        private readonly IBehavioralInsightsRepository behavioralInsightsRepository = behavioralInsightsRepository;
        private readonly ITransactionsRepository transactionsRepository = transactionsRepository;
        private readonly ILogger<BehavioralInsightsService> logger = logger;

        public async Task<long> CreateAsync(CreateBehavioralInsightDto dto)
        {
            try
            {
                Domain.Validate.NotNull(dto, nameof(CreateBehavioralInsightDto));
                dto.UserId = currentUser.ResolveUserId(dto.UserId);
                Domain.Validate.Id(dto.UserId, nameof(dto.UserId));
                if (dto.CategoryId.HasValue)
                    Domain.Validate.Id(dto.CategoryId.Value, nameof(dto.CategoryId));
                Domain.Validate.Enum(dto.Type, nameof(dto.Type));
                Domain.Validate.String(dto.Description, nameof(dto.Description), minLength: 2, maxLength: 500);
                Domain.Validate.DateOnly(dto.ReferenceDate, nameof(dto.ReferenceDate));

                return await behavioralInsightsRepository.CreateAsync(dto);
            }
            catch (DomainValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao criar insight para o usuário {UserId}", dto.UserId);
                throw;
            }
        }

        public async Task<BehavioralInsightResult> GetByIdAsync(long id)
        {
            try
            {
                Domain.Validate.Id(id, nameof(id));

                var behavioralInsight = await GetOwnedAsync(id);

                return behavioralInsight.Adapt<BehavioralInsightResult>();
            }
            catch (Exception ex) when (ex is DomainValidationException or ForbiddenException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao buscar insight {InsightId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<BehavioralInsightResult>> GetAllByUserIdAsync(long userId, bool includeOutdated = false)
        {
            try
            {
                Domain.Validate.Id(userId, nameof(userId));
                currentUser.EnsureCanAccess(userId);

                var insights = await behavioralInsightsRepository.GetAllByUserIdAsync(userId, includeOutdated);
                return insights.Adapt<IEnumerable<BehavioralInsightResult>>();
            }
            catch (Exception ex) when (ex is DomainValidationException or ForbiddenException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao buscar insights do usuário {UserId}", userId);
                throw;
            }
        }

        public async Task UpdateAsync(long id, UpdateBehavioralInsightDto dto)
        {
            try
            {
                Domain.Validate.NotNull(dto, nameof(UpdateBehavioralInsightDto));
                if (id != dto.Id)
                    throw new DomainValidationException("O id da rota é diferente do id enviado no corpo.");
                Domain.Validate.Id(dto.Id, nameof(dto.Id));

                await GetOwnedAsync(dto.Id);

                if (!dto.CategoryId.HasValue)
                    throw new DomainValidationException($"{nameof(dto.CategoryId)} is required.");
                Domain.Validate.Id(dto.CategoryId.Value, nameof(dto.CategoryId));

                Domain.Validate.Enum(dto.Type, nameof(dto.Type));
                Domain.Validate.String(dto.Description, nameof(dto.Description), minLength: 2, maxLength: 500);
                Domain.Validate.DateOnly(dto.ReferenceDate, nameof(dto.ReferenceDate));

                if (dto.ViewedAt.HasValue)
                    Domain.Validate.DateTime(dto.ViewedAt.Value, nameof(dto.ViewedAt), max: DateTime.UtcNow);

                if (dto.Feedback.HasValue)
                    Domain.Validate.Enum(dto.Feedback.Value, nameof(dto.Feedback));

                await behavioralInsightsRepository.UpdateAsync(dto);
            }
            catch (Exception ex) when (ex is DomainValidationException or ForbiddenException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao atualizar insight {InsightId}", id);
                throw;
            }
        }

        public async Task MarkViewedAsync(long id)
        {
            try
            {
                Domain.Validate.Id(id, nameof(id));

                var insight = await GetOwnedAsync(id);

                var now = DateTime.UtcNow;
                UpdateBehavioralInsightViewedAsync dto = new()
                {
                    Id = id,
                    UserId = insight.UserId,
                    ViewedAt = now,
                    WasInteracted = true,
                };

                Domain.Validate.Id(dto.UserId, nameof(dto.UserId));
                Domain.Validate.DateTime(dto.ViewedAt, nameof(dto.ViewedAt), max: DateTime.UtcNow);
                Domain.Validate.Bool(dto.WasInteracted, nameof(dto.WasInteracted), expected: true);

                await behavioralInsightsRepository.MarkViewedAsync(dto);
            }
            catch (Exception ex) when (ex is DomainValidationException or ForbiddenException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao marcar insight como visualizado {InsightId}", id);
                throw;
            }
        }

        public async Task UpdateFeedbackAsync(long id, UpdateBehavioralInsightFeedbackDto dto)
        {
            try
            {
                Domain.Validate.Id(id, nameof(id));
                Domain.Validate.NotNull(dto, nameof(UpdateBehavioralInsightFeedbackDto));
                if (!dto.Feedback.HasValue)
                    throw new DomainValidationException("Informe o feedback.");
                Domain.Validate.Enum(dto.Feedback.Value, nameof(dto.Feedback));

                await GetOwnedAsync(id);

                await behavioralInsightsRepository.UpdateFeedbackAsync(id, dto.Feedback.Value);
            }
            catch (Exception ex) when (ex is DomainValidationException or ForbiddenException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao salvar o feedback do insight {InsightId}", id);
                throw;
            }
        }

        private static bool IsSameInsight(BehavioralInsight a, BehavioralInsight b) =>
            a.RuleCode is not null
            && a.RuleCode == b.RuleCode
            && a.RelatedEntityId == b.RelatedEntityId
            && a.ReferenceDate == b.ReferenceDate;

        public async Task EvaluateActionTakenAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            try
            {
                var insight = await GetOwnedAsync(id);

                if (insight.Type != BehavioralInsightTypes.alerta_gasto && insight.Type != BehavioralInsightTypes.sugestao)
                    return;

                if (insight.ViewedAt is null)
                    return;

                if (insight.CategoryId is null)
                    throw new DomainValidationException("O insight não possui categoria associada; não é possível calcular ActionTaken.");

                var categoryId = insight.CategoryId.Value;
                var windowStart = insight.ViewedAt.Value;
                var windowEnd = windowStart.AddDays(30);

                var overlapping = (await behavioralInsightsRepository.GetOverlappingAsync(
                        insight.UserId, categoryId, windowStart, windowEnd, insight.Id))
                    .Where(o => !IsSameInsight(o, insight))
                    .ToList();

                if (overlapping.Count > 0)
                {
                    var idsParaInvalidar = overlapping.Select(o => o.Id).Append(insight.Id);
                    await behavioralInsightsRepository.MarkAsNonEvaluableAsync(idsParaInvalidar);
                    return;
                }

                var referenceMonthStart = new DateOnly(insight.ReferenceDate.Year, insight.ReferenceDate.Month, 1);
                var baselineStart = referenceMonthStart.AddMonths(-2);
                var baselineEnd = referenceMonthStart.AddDays(-1);

                GetCategoryTotalsByPeriodDto getTotalByCategoryAndPeriodDto = new()
                {
                    userId = insight.UserId,
                    categoryId = categoryId,
                    startDate = baselineStart,
                    endDate = baselineEnd
                };
                var baselineTotalCents = await transactionsRepository.GetTotalByCategoryAndPeriodAsync(getTotalByCategoryAndPeriodDto);
                var baselineMonthlyAverage = baselineTotalCents / 2m;

                GetCategoryTotalsByPeriodDto getCategoryTotalsByPeriodDto = new()
                {
                    userId = insight.UserId,
                    categoryId = categoryId,
                    startDate = DateOnly.FromDateTime(windowStart),
                    endDate = DateOnly.FromDateTime(windowEnd)
                };
                var actualCents = await transactionsRepository.GetTotalByCategoryAndPeriodAsync(getCategoryTotalsByPeriodDto);

                bool actionTaken;
                if (baselineMonthlyAverage <= 0)
                {
                    actionTaken = false;
                }
                else
                {
                    var dropRatio = (baselineMonthlyAverage - actualCents) / baselineMonthlyAverage;
                    actionTaken = dropRatio >= MinimumDropThreshold;
                }

                var updateDto = new UpdateBehavioralInsightDto
                {
                    Id = insight.Id,
                    CategoryId = insight.CategoryId,
                    Type = insight.Type,
                    Description = insight.Description,
                    ReferenceDate = insight.ReferenceDate,
                    ViewedAt = insight.ViewedAt,
                    Feedback = insight.Feedback,
                    ActionTaken = actionTaken,
                    ActionTakenEvaluatedAt = DateTime.UtcNow,
                };

                await behavioralInsightsRepository.UpdateAsync(updateDto);
            }
            catch (Exception ex) when (ex is DomainValidationException or ForbiddenException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao avaliar ActionTaken para o insight {InsightId}", id);
                throw;
            }
        }

        private async Task<BehavioralInsight> GetOwnedAsync(long id)
        {
            var insight = await behavioralInsightsRepository.GetByIdAsync(id)
                ?? throw new DomainValidationException("Insight não encontrado.");
            currentUser.EnsureCanAccess(insight.UserId);

            return insight;
        }

        public async Task<bool> DeleteAsync(long id)
        {
            try
            {
                Domain.Validate.Id(id, nameof(id));
                return await behavioralInsightsRepository.DeleteAsync(id);
            }
            catch (DomainValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao deletar insight {InsightId}", id);
                throw;
            }
        }
    }
}