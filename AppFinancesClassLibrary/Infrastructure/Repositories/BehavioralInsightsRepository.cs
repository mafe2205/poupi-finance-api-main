using AppFinancesClassLibrary.Application.BehavioralInsights.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class BehavioralInsightsRepository(IUnitOfWork unitOfWork) : IBehavioralInsightsRepository
    {
        private IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetById = @"
            SELECT ID, USER_ID, CATEGORY_ID, TYPE, RULE_CODE, RELATED_ENTITY_ID, DESCRIPTION, REFERENCE_DATE, WAS_INTERACTED, VIEWED_AT, FEEDBACK, ACTION_TAKEN, ACTION_TAKEN_EVALUATED_AT, FACTS, OUTDATED_AT, OUTDATED_REASON, CREATED_AT, UPDATED_AT
            FROM BEHAVIORAL_INSIGHTS
            WHERE ID = :Id";

        private const string sqlGetAllByUserId = @"
            SELECT ID, USER_ID, CATEGORY_ID, TYPE, RULE_CODE, RELATED_ENTITY_ID, DESCRIPTION, REFERENCE_DATE, WAS_INTERACTED, VIEWED_AT, FEEDBACK, ACTION_TAKEN, ACTION_TAKEN_EVALUATED_AT, FACTS, OUTDATED_AT, OUTDATED_REASON, CREATED_AT, UPDATED_AT
            FROM BEHAVIORAL_INSIGHTS
            WHERE USER_ID = :UserId
                AND (:IncludeOutdated OR OUTDATED_AT IS NULL)
            ORDER BY REFERENCE_DATE DESC";

        // Insights atuais gerados por regra: os que a atualização da análise confere contra os dados.
        private const string sqlGetCurrentRuleInsights = @"
            SELECT ID, USER_ID, CATEGORY_ID, TYPE, RULE_CODE, RELATED_ENTITY_ID, DESCRIPTION, REFERENCE_DATE, WAS_INTERACTED, VIEWED_AT, FEEDBACK, ACTION_TAKEN, ACTION_TAKEN_EVALUATED_AT, FACTS, OUTDATED_AT, OUTDATED_REASON, CREATED_AT, UPDATED_AT
            FROM BEHAVIORAL_INSIGHTS
            WHERE USER_ID = :UserId AND RULE_CODE IS NOT NULL AND OUTDATED_AT IS NULL";

        private const string sqlMarkOutdated = @"
            UPDATE BEHAVIORAL_INSIGHTS
            SET OUTDATED_AT = :OutdatedAt, OUTDATED_REASON = :OutdatedReason, UPDATED_AT = :OutdatedAt
            WHERE ID = :Id AND OUTDATED_AT IS NULL";

        private const string sqlDelete = "DELETE FROM BEHAVIORAL_INSIGHTS WHERE ID = :Id";

        private const string sqlInsert = @"
            INSERT INTO BEHAVIORAL_INSIGHTS (USER_ID, CATEGORY_ID, TYPE, RULE_CODE, RELATED_ENTITY_ID, DESCRIPTION, REFERENCE_DATE, WAS_INTERACTED, VIEWED_AT, FEEDBACK, ACTION_TAKEN, FACTS, CREATED_AT, UPDATED_AT)
            VALUES (:UserId, :CategoryId, :Type, :RuleCode, :RelatedEntityId, :Description, :ReferenceDate, :WasInteracted, :ViewedAt, :Feedback, :ActionTaken, :Facts, :CreatedAt, :UpdatedAt)
            RETURNING ID";

        private const string sqlUpdate = @"
            UPDATE BEHAVIORAL_INSIGHTS SET
                CATEGORY_ID = :CategoryId, TYPE = :Type, DESCRIPTION = :Description, REFERENCE_DATE = :ReferenceDate,
                VIEWED_AT = :ViewedAt, FEEDBACK = :Feedback, ACTION_TAKEN = :ActionTaken, ACTION_TAKEN_EVALUATED_AT = :ActionTakenEvaluatedAt, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id
            RETURNING ID, USER_ID, CATEGORY_ID, TYPE, RULE_CODE, RELATED_ENTITY_ID, DESCRIPTION, REFERENCE_DATE, WAS_INTERACTED, VIEWED_AT, FEEDBACK, ACTION_TAKEN, ACTION_TAKEN_EVALUATED_AT, FACTS, OUTDATED_AT, OUTDATED_REASON, CREATED_AT, UPDATED_AT";

        private const string sqlUpdateFeedback = @"
            UPDATE BEHAVIORAL_INSIGHTS SET
                FEEDBACK = :Feedback, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id
            RETURNING ID, USER_ID, CATEGORY_ID, TYPE, RULE_CODE, RELATED_ENTITY_ID, DESCRIPTION, REFERENCE_DATE, WAS_INTERACTED, VIEWED_AT, FEEDBACK, ACTION_TAKEN, ACTION_TAKEN_EVALUATED_AT, FACTS, OUTDATED_AT, OUTDATED_REASON, CREATED_AT, UPDATED_AT";

        private const string sqlGetOverlapping = @"
            SELECT ID, USER_ID, CATEGORY_ID, TYPE, RULE_CODE, RELATED_ENTITY_ID, DESCRIPTION, REFERENCE_DATE, WAS_INTERACTED, VIEWED_AT, FEEDBACK, ACTION_TAKEN, ACTION_TAKEN_EVALUATED_AT, FACTS, OUTDATED_AT, OUTDATED_REASON, CREATED_AT, UPDATED_AT
            FROM BEHAVIORAL_INSIGHTS
            WHERE USER_ID = :UserId AND CATEGORY_ID = :CategoryId AND ID != :ExcludeId
            AND TYPE IN (:AlertaGastoType, :SugestaoType) AND VIEWED_AT IS NOT NULL
            AND VIEWED_AT <= :WindowEnd AND (VIEWED_AT + INTERVAL '30 days') >= :WindowStart";

        // ActionTaken mede a queda do gasto na categoria do insight: insights sem categoria
        // (metas, cartões, contas, fluxo) não têm o que medir e ficam fora do lote.
        private const string sqlGetPendingActionTakenEvaluation = @"
            SELECT ID FROM BEHAVIORAL_INSIGHTS
            WHERE TYPE IN (:AlertaGastoType, :SugestaoType) AND CATEGORY_ID IS NOT NULL
                AND VIEWED_AT IS NOT NULL AND VIEWED_AT <= :ViewedBefore
                AND ACTION_TAKEN_EVALUATED_AT IS NULL";

        private const string sqlMarkAsViewed = @"
            UPDATE BEHAVIORAL_INSIGHTS
            SET VIEWED_AT = :ViewedAt,
                WAS_INTERACTED = TRUE,
                UPDATED_AT = :UpdatedAt
            WHERE ID = :Id
                AND USER_ID = :UserId
                AND VIEWED_AT IS NULL";

        private const string sqlMaskAsNonEvaluable = @"
            UPDATE BEHAVIORAL_INSIGHTS
            SET ACTION_TAKEN = NULL,
                ACTION_TAKEN_EVALUATED_AT = :UpdatedAt,
                UPDATED_AT = :UpdatedAt
            WHERE ID IN :InsightIds";

        private const string sqlCountGeneratedInPeriod = @"
            SELECT COUNT(*) FROM BEHAVIORAL_INSIGHTS
            WHERE USER_ID = :UserId
                AND REFERENCE_DATE >= :From AND REFERENCE_DATE <= :To";

        private const string sqlCountInteractedInPeriod = @"
            SELECT COUNT(*) FROM BEHAVIORAL_INSIGHTS
            WHERE USER_ID = :UserId AND WAS_INTERACTED = true
                AND REFERENCE_DATE >= :From AND REFERENCE_DATE <= :To";
        #endregion

        public Task<BehavioralInsight> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<BehavioralInsight>(sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<BehavioralInsight>> GetAllByUserIdAsync(long userId, bool includeOutdated = false)
        {
            return unitOfWork.Connection.QueryAsync<BehavioralInsight>(sqlGetAllByUserId,
                new { UserId = userId, IncludeOutdated = includeOutdated }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<BehavioralInsight>> GetCurrentRuleInsightsAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<BehavioralInsight>(sqlGetCurrentRuleInsights, new { UserId = userId }, unitOfWork.Transaction);
        }

        public Task MarkOutdatedAsync(long id, string reason)
        {
            return unitOfWork.Connection.ExecuteAsync(sqlMarkOutdated,
                new { Id = id, OutdatedReason = reason, OutdatedAt = DateTime.UtcNow }, unitOfWork.Transaction);
        }

        public Task<long> CreateAsync(CreateBehavioralInsightDto dto)
        {
            var now = DateTime.UtcNow;

            var param = new
            {
                dto.UserId,
                dto.CategoryId,
                Type = dto.Type.ToString(),
                dto.RuleCode,
                dto.RelatedEntityId,
                dto.Description,
                dto.ReferenceDate,
                WasInteracted = false,
                ViewedAt = (DateTime?)null,
                Feedback = (string?)null,
                ActionTaken = (bool?)null,
                dto.Facts,
                CreatedAt = now,
                UpdatedAt = now
            };

            return unitOfWork.Connection.ExecuteScalarAsync<long>(sqlInsert, param, unitOfWork.Transaction);
        }

        public Task<BehavioralInsight> UpdateAsync(UpdateBehavioralInsightDto dto)
        {
            var param = new
            {
                dto.Id,
                dto.CategoryId,
                Type = dto.Type.ToString(),
                dto.Description,
                dto.ReferenceDate,
                dto.ViewedAt,
                Feedback = dto.Feedback.HasValue ? dto.Feedback.Value.ToString() : null,
                dto.ActionTaken,
                dto.ActionTakenEvaluatedAt,
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleAsync<BehavioralInsight>(sqlUpdate, param, unitOfWork.Transaction);
        }

        public Task<IEnumerable<long>> GetPendingActionTakenEvaluationAsync(DateTime viewedBefore)
        {
            return unitOfWork.Connection.QueryAsync<long>(sqlGetPendingActionTakenEvaluation, new
            {
                AlertaGastoType = BehavioralInsightTypes.alerta_gasto.ToString(),
                SugestaoType = BehavioralInsightTypes.sugestao.ToString(),
                ViewedBefore = viewedBefore
            }, unitOfWork.Transaction);
        }

        public Task<BehavioralInsight> UpdateFeedbackAsync(long Id, Feedback feedback)
        {
            var param = new
            {
                Id,
                Feedback = feedback.ToString(),
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleAsync<BehavioralInsight>(sqlUpdateFeedback, param, unitOfWork.Transaction);
        }

        public Task<IEnumerable<BehavioralInsight>> GetOverlappingAsync(long userId, long categoryId, DateTime windowStart, DateTime windowEnd, long excludeId)
        {
            return unitOfWork.Connection.QueryAsync<BehavioralInsight>(sqlGetOverlapping, new
            {
                UserId = userId,
                CategoryId = categoryId,
                ExcludeId = excludeId,
                AlertaGastoType = BehavioralInsightTypes.alerta_gasto.ToString(),
                SugestaoType = BehavioralInsightTypes.sugestao.ToString(),
                WindowStart = windowStart,
                WindowEnd = windowEnd
            }, unitOfWork.Transaction);
        }

        public Task MarkViewedAsync(UpdateBehavioralInsightViewedAsync dto)
        {
            var param = new
            {
                dto.Id,
                dto.UserId,
                dto.ViewedAt,
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.ExecuteAsync(sqlMarkAsViewed, param, unitOfWork.Transaction);
        }

        public Task MarkAsNonEvaluableAsync(IEnumerable<long> insightIds)
        {
            var ids = insightIds.ToList();
            if (ids.Count == 0) return Task.CompletedTask;

            var param = new
            {
                InsightIds = ids,
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.ExecuteAsync(sqlMaskAsNonEvaluable, param, unitOfWork.Transaction);
        }

        public Task<int> CountGeneratedInPeriodAsync(long userId, DateOnly from, DateOnly to)
        {
            return unitOfWork.Connection.ExecuteScalarAsync<int>(sqlCountGeneratedInPeriod,
                new { UserId = userId, From = from, To = to }, unitOfWork.Transaction);
        }

        public Task<int> CountInteractedInPeriodAsync(long userId, DateOnly from, DateOnly to)
        {
            return unitOfWork.Connection.ExecuteScalarAsync<int>(sqlCountInteractedInPeriod,
                new { UserId = userId, From = from, To = to }, unitOfWork.Transaction);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var rowsAffected = await unitOfWork.Connection.ExecuteAsync(sqlDelete, new { Id = id }, unitOfWork.Transaction);
            return rowsAffected > 0;
        }
    }
}