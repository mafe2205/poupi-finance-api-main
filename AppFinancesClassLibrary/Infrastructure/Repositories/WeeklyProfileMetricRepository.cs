using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.WeeklyProfileMetric.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class WeeklyProfileMetricRepository(IUnitOfWork unitOfWork) : IWeeklyProfileMetricRepository
    {
        private IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlInsert = @"
            INSERT INTO WEEKLY_PROFILE_METRICS (
                USER_ID, WEEK_REFERENCE, TOTAL_EXPENSES_CENTS, TOTAL_REVENUE_CENTS,
                PERCENTAGE_EXPENSE_RELATIVE_REVENUE, QUANTITY_LATE_TRANSACTIONS,
                QUANTITY_INSIGHTS_GENERATED, QUANTITY_INSIGHTS_INTERACTED, CREATED_AT, UPDATED_AT)
            VALUES (
                :UserId, :WeekReference, :TotalExpensesCents, :TotalRevenueCents,
                :PercentageExpenseRelativeRevenue, :QuantityLateTransactions,
                :QuantityInsightsGenerated, :QuantityInsightsInteracted, :CreatedAt, :UpdatedAt)
            RETURNING ID, USER_ID, WEEK_REFERENCE, TOTAL_EXPENSES_CENTS, TOTAL_REVENUE_CENTS,
                PERCENTAGE_EXPENSE_RELATIVE_REVENUE, QUANTITY_LATE_TRANSACTIONS,
                QUANTITY_INSIGHTS_GENERATED, QUANTITY_INSIGHTS_INTERACTED, CREATED_AT, UPDATED_AT";

        private const string sqlGetById = @"
           SELECT ID, USER_ID, WEEK_REFERENCE, TOTAL_EXPENSES_CENTS, TOTAL_REVENUE_CENTS,
                PERCENTAGE_EXPENSE_RELATIVE_REVENUE, QUANTITY_LATE_TRANSACTIONS,
                QUANTITY_INSIGHTS_GENERATED, QUANTITY_INSIGHTS_INTERACTED, CREATED_AT, UPDATED_AT
           FROM WEEKLY_PROFILE_METRICS
           WHERE ID = :Id";

        private const string sqlUpdate = @"
            UPDATE WEEKLY_PROFILE_METRICS SET
                WEEK_REFERENCE = :WeekReference, TOTAL_EXPENSES_CENTS = :TotalExpensesCents,
                TOTAL_REVENUE_CENTS = :TotalRevenueCents, PERCENTAGE_EXPENSE_RELATIVE_REVENUE = :PercentageExpenseRelativeRevenue,
                QUANTITY_LATE_TRANSACTIONS = :QuantityLateTransactions, QUANTITY_INSIGHTS_GENERATED = :QuantityInsightsGenerated,
                QUANTITY_INSIGHTS_INTERACTED = :QuantityInsightsInteracted, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id";

        private const string sqlGetByUserAndWeekRange = @"
            SELECT
                ID,
                USER_ID,
                WEEK_REFERENCE,
                TOTAL_EXPENSES_CENTS,
                TOTAL_REVENUE_CENTS,
                PERCENTAGE_EXPENSE_RELATIVE_REVENUE,
                QUANTITY_LATE_TRANSACTIONS,
                QUANTITY_INSIGHTS_GENERATED,
                QUANTITY_INSIGHTS_INTERACTED,
                CREATED_AT,
                UPDATED_AT
            FROM WEEKLY_PROFILE_METRICS
            WHERE USER_ID = :UserId
            AND (:StartWeek IS NULL OR WEEK_REFERENCE >= :StartWeek)
            AND (:EndWeek IS NULL OR WEEK_REFERENCE <= :EndWeek)
            ORDER BY WEEK_REFERENCE";

        private const string sqlGetByUserAndWeek = @"
        SELECT ID, USER_ID, WEEK_REFERENCE, TOTAL_EXPENSES_CENTS, TOTAL_REVENUE_CENTS,
                PERCENTAGE_EXPENSE_RELATIVE_REVENUE, QUANTITY_LATE_TRANSACTIONS,
                QUANTITY_INSIGHTS_GENERATED, QUANTITY_INSIGHTS_INTERACTED, CREATED_AT, UPDATED_AT
            FROM WEEKLY_PROFILE_METRICS
            WHERE USER_ID = :UserId AND WEEK_REFERENCE = :WeekReference";
        #endregion

        public Task<WeeklyProfileMetric> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<WeeklyProfileMetric>(sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<WeeklyProfileMetric> CreateAsync(CreateWeeklyProfileMetricDto dto)
        {
            var now = DateTime.UtcNow;

            var param = new
            {
                dto.UserId,
                dto.WeekReference,
                dto.TotalExpensesCents,
                dto.TotalRevenueCents,
                dto.PercentageExpenseRelativeRevenue,
                dto.QuantityLateTransactions,
                dto.QuantityInsightsGenerated,
                dto.QuantityInsightsInteracted,
                CreatedAt = now,
                UpdatedAt = now
            };

            return unitOfWork.Connection.QuerySingleAsync<WeeklyProfileMetric>(sqlInsert, param, unitOfWork.Transaction);
        }

        public async Task UpdateAsync(UpdateWeeklyProfileMetricDto dto)
        {
            var param = new
            {
                dto.Id,
                dto.WeekReference,
                dto.TotalExpensesCents,
                dto.TotalRevenueCents,
                dto.PercentageExpenseRelativeRevenue,
                dto.QuantityLateTransactions,
                dto.QuantityInsightsGenerated,
                dto.QuantityInsightsInteracted,
                UpdatedAt = DateTime.UtcNow
            };

            await unitOfWork.Connection.ExecuteAsync(sqlUpdate, param, unitOfWork.Transaction);
        }

        public Task<IEnumerable<WeeklyProfileMetricResult>> GetWeeklyMetricsAsync(long userId, string startWeek, string endWeek)
        {
            return unitOfWork.Connection.QueryAsync<WeeklyProfileMetricResult>(sqlGetByUserAndWeekRange, new
            {
                UserId = userId,
                StartWeek = startWeek,
                EndWeek = endWeek
            }, unitOfWork.Transaction);
        }

        public Task<WeeklyProfileMetricResult> GetByUserAndWeekAsync(long userId, string weekReference)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<WeeklyProfileMetricResult>(
                sqlGetByUserAndWeek,
                new { UserId = userId, WeekReference = weekReference }, unitOfWork.Transaction);
        }
    }
}