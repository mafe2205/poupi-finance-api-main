using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.WeeklyProfileMetric.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;

namespace AppFinancesClassLibrary.Application.WeeklyProfileMetric.Services
{
    public class WeeklyProfileMetricService(IWeeklyProfileMetricRepository weeklyProfileMetricRepository, ICurrentUser currentUser) : IWeeklyProfileMetricService
    {
        private readonly IWeeklyProfileMetricRepository weeklyProfileMetricRepository = weeklyProfileMetricRepository;

        public async Task<WeeklyProfileMetricResult> CreateAsync(CreateWeeklyProfileMetricDto dto)
        {
            Domain.Validate.NotNull(dto, nameof(CreateWeeklyProfileMetricDto));

            dto.UserId = currentUser.ResolveUserId(dto.UserId);
            Domain.Validate.Id(dto.UserId, nameof(dto.UserId));
            Domain.Validate.String(dto.WeekReference, nameof(dto.WeekReference));
            Domain.Validate.Int(dto.TotalExpensesCents, nameof(dto.TotalExpensesCents), min: 0);
            Domain.Validate.Int(dto.TotalRevenueCents, nameof(dto.TotalRevenueCents), min: 0);
            Domain.Validate.Decimal(dto.PercentageExpenseRelativeRevenue, nameof(dto.PercentageExpenseRelativeRevenue), min: 0);
            Domain.Validate.Int(dto.QuantityLateTransactions, nameof(dto.QuantityLateTransactions), min: 0);
            Domain.Validate.Int(dto.QuantityInsightsGenerated, nameof(dto.QuantityInsightsGenerated), min: 0);
            Domain.Validate.Int(dto.QuantityInsightsInteracted, nameof(dto.QuantityInsightsInteracted), min: 0);

            Domain.Entities.WeeklyProfileMetric weeklyProfileMetric = await weeklyProfileMetricRepository.CreateAsync(dto);
            return weeklyProfileMetric.Adapt<WeeklyProfileMetricResult>();
        }

        public async Task<WeeklyProfileMetricResult> GetByIdAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            Domain.Entities.WeeklyProfileMetric weeklyProfileMetric = await GetOwnedAsync(id);

            return weeklyProfileMetric.Adapt<WeeklyProfileMetricResult>();
        }

        public async Task UpdateAsync(UpdateWeeklyProfileMetricDto dto)
        {
            Domain.Validate.NotNull(dto, nameof(UpdateWeeklyProfileMetricDto));
            Domain.Validate.Id(dto.Id, nameof(dto.Id));

            dto.UserId = (await GetOwnedAsync(dto.Id)).UserId;

            Domain.Validate.Id(dto.UserId, nameof(dto.UserId));
            Domain.Validate.String(dto.WeekReference, nameof(dto.WeekReference));
            Domain.Validate.Int(dto.TotalExpensesCents, nameof(dto.TotalExpensesCents), min: 0);
            Domain.Validate.Int(dto.TotalRevenueCents, nameof(dto.TotalRevenueCents), min: 0);
            Domain.Validate.Decimal(dto.PercentageExpenseRelativeRevenue, nameof(dto.PercentageExpenseRelativeRevenue), min: 0);
            Domain.Validate.Int(dto.QuantityLateTransactions, nameof(dto.QuantityLateTransactions), min: 0);
            Domain.Validate.Int(dto.QuantityInsightsGenerated, nameof(dto.QuantityInsightsGenerated), min: 0);
            Domain.Validate.Int(dto.QuantityInsightsInteracted, nameof(dto.QuantityInsightsInteracted), min: 0);

            await weeklyProfileMetricRepository.UpdateAsync(dto);
        }

        public async Task<IEnumerable<WeeklyProfileMetricResult>> GetWeeklyMetricsAsync(
            long userId, string startWeek, string endWeek)
        {
            Domain.Validate.Id(userId, nameof(userId));
            currentUser.EnsureCanAccess(userId);

            if (!string.IsNullOrWhiteSpace(startWeek))
                Domain.Validate.String(startWeek, nameof(startWeek));

            if (!string.IsNullOrWhiteSpace(endWeek))
                Domain.Validate.String(endWeek, nameof(endWeek));

            if (!string.IsNullOrWhiteSpace(startWeek) && !string.IsNullOrWhiteSpace(endWeek)
                && string.Compare(startWeek, endWeek, StringComparison.Ordinal) > 0)
            {
                throw new DomainValidationException($"{nameof(startWeek)} não pode ser posterior a {nameof(endWeek)}.");
            }

            IEnumerable<WeeklyProfileMetricResult> metrics = await weeklyProfileMetricRepository.GetWeeklyMetricsAsync(userId, startWeek, endWeek) ?? [];
            return metrics;
        }

        private async Task<Domain.Entities.WeeklyProfileMetric> GetOwnedAsync(long id)
        {
            Domain.Entities.WeeklyProfileMetric weeklyProfileMetric = await weeklyProfileMetricRepository.GetByIdAsync(id)
                ?? throw new DomainValidationException("Métrica de perfil semanal não encontrada.");
            currentUser.EnsureCanAccess(weeklyProfileMetric.UserId);

            return weeklyProfileMetric;
        }
    }
}