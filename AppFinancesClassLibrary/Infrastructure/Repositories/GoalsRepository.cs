using AppFinancesClassLibrary.Application.Goals.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class GoalsRepository(IUnitOfWork unitOfWork) : IGoalsRepository
    {
        private IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetById = @"
            SELECT ID, USER_ID, INICIAL_DATE, FINAL_DATE, INICIAL_VALUE_CENTS, FINAL_VALUE_CENTS, DESCRIPTION, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM GOALS
            WHERE ID = :Id AND ACTIVE";

        private const string sqlGetAllByUserId = @"
            SELECT ID, USER_ID, INICIAL_DATE, FINAL_DATE, INICIAL_VALUE_CENTS, FINAL_VALUE_CENTS, DESCRIPTION, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM GOALS
            WHERE USER_ID = :UserId AND ACTIVE
            ORDER BY INICIAL_DATE DESC";

        private const string sqlInsert = @"
            INSERT INTO GOALS (USER_ID, INICIAL_DATE, FINAL_DATE, INICIAL_VALUE_CENTS, FINAL_VALUE_CENTS, DESCRIPTION, CREATED_AT, UPDATED_AT)
            VALUES (:UserId, :InicialDate, :FinalDate, :InicialValueCents, :FinalValueCents, :Description, :CreatedAt, :UpdatedAt)
            RETURNING ID, USER_ID, INICIAL_DATE, FINAL_DATE, INICIAL_VALUE_CENTS, FINAL_VALUE_CENTS, DESCRIPTION, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        private const string sqlUpdate = @"
            UPDATE GOALS SET
                FINAL_DATE = COALESCE(:FinalDate, FINAL_DATE),
                FINAL_VALUE_CENTS = CASE WHEN :FinalValueCents = 0 THEN FINAL_VALUE_CENTS ELSE :FinalValueCents END,
                DESCRIPTION = COALESCE(:Description, DESCRIPTION),
                UPDATED_AT = :UpdatedAt
            WHERE ID = :Id AND ACTIVE
            RETURNING ID, USER_ID, INICIAL_DATE, FINAL_DATE, INICIAL_VALUE_CENTS, FINAL_VALUE_CENTS, DESCRIPTION, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        private const string sqlDelete = @"
            UPDATE GOALS SET ACTIVE = false, DELETED_AT = :DeletedAt, UPDATED_AT = :DeletedAt
            WHERE ID = :Id AND ACTIVE
            RETURNING ID, USER_ID, INICIAL_DATE, FINAL_DATE, INICIAL_VALUE_CENTS, FINAL_VALUE_CENTS, DESCRIPTION, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";
        #endregion

        public Task<Goal> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Goal>(sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<Goal>> GetAllByUserIdAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<Goal>(sqlGetAllByUserId, new { UserId = userId }, unitOfWork.Transaction);
        }

        public Task<Goal> CreateAsync(CreateGoalDto dto)
        {
            var now = DateTime.UtcNow;

            var param = new
            {
                dto.UserId,
                dto.InicialDate,
                dto.FinalDate,
                dto.InicialValueCents,
                dto.FinalValueCents,
                dto.Description,
                CreatedAt = now,
                UpdatedAt = now
            };

            return unitOfWork.Connection.QuerySingleAsync<Goal>(sqlInsert, param, unitOfWork.Transaction);
        }

        public Task<Goal> UpdateAsync(UpdateGoalDto dto)
        {
            var param = new
            {
                dto.Id,
                FinalDate = dto.FinalDate == default ? (DateOnly?)null : dto.FinalDate,
                dto.FinalValueCents,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description,
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleAsync<Goal>(sqlUpdate, param, unitOfWork.Transaction);
        }

        public Task<Goal?> DeleteAsync(long id)
        {
            var param = new { Id = id, DeletedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleOrDefaultAsync<Goal?>(sqlDelete, param, unitOfWork.Transaction);
        }
    }
}