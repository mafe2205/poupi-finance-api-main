using AppFinancesClassLibrary.Application.Categories.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class CategoriesRepository(IUnitOfWork unitOfWork) : ICategoriesRepository
    {
        private IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetById = @"
            SELECT ID, USER_ID, NAME, TYPE, IS_DEFAULT, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM CATEGORIES
            WHERE ID = :Id AND ACTIVE";

        private const string sqlGetByIdIncludingInactive = @"
            SELECT ID, USER_ID, NAME, TYPE, IS_DEFAULT, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM CATEGORIES
            WHERE ID = :Id";

        private const string sqlGetAllByUserId = @"
            SELECT ID, USER_ID, NAME, TYPE, IS_DEFAULT, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM CATEGORIES
            WHERE USER_ID = :UserId AND ACTIVE
            ORDER BY NAME";

        private const string sqlGetAllInactiveByUserId = @"
            SELECT ID, USER_ID, NAME, TYPE, IS_DEFAULT, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM CATEGORIES
            WHERE USER_ID = :UserId AND NOT ACTIVE
            ORDER BY NAME";

        private const string sqlReactivate = @"
            UPDATE CATEGORIES SET ACTIVE = true, DELETED_AT = NULL, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id AND NOT ACTIVE
            RETURNING ID, USER_ID, NAME, TYPE, IS_DEFAULT, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        private const string sqlCreate = @"
            INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, IS_DEFAULT, LIMIT_VALUE_CENTS, CREATED_AT)
            VALUES (:UserId, :Name, :Type, :IsDefault, :LimitValueCents, :CreatedAt)
            RETURNING ID, USER_ID, NAME, TYPE, IS_DEFAULT, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        private const string sqlUpdate = @"
            UPDATE CATEGORIES SET
                NAME = COALESCE(:Name, NAME),
                LIMIT_VALUE_CENTS = CASE WHEN :LimitValueCents = 0 THEN LIMIT_VALUE_CENTS ELSE :LimitValueCents END,
                UPDATED_AT = :UpdatedAt
            WHERE ID = :Id AND ACTIVE
            RETURNING ID, USER_ID, NAME, TYPE, IS_DEFAULT, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        private const string sqlDelete = @"
            UPDATE CATEGORIES SET ACTIVE = false, DELETED_AT = :DeletedAt, UPDATED_AT = :DeletedAt
            WHERE ID = :Id AND ACTIVE
            RETURNING ID, USER_ID, NAME, TYPE, IS_DEFAULT, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";
        #endregion

        public Task<Category> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Category>(sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<Category?> GetByIdIncludingInactiveAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Category?>(sqlGetByIdIncludingInactive, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<Category>> GetAllByUserIdAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<Category>(sqlGetAllByUserId, new { UserId = userId }, unitOfWork.Transaction);
        }

        public Task<Category> CreateAsync(CreateCategoryDto dto)
        {
            var param = new
            {
                dto.UserId,
                dto.Name,
                dto.Type,
                dto.IsDefault,
                dto.LimitValueCents,
                CreatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleAsync<Category>(sqlCreate, param, unitOfWork.Transaction);
        }

        public Task<Category> UpdateAsync(UpdateCategoryDto dto)
        {
            var param = new
            {
                dto.Id,
                dto.Name,
                dto.LimitValueCents,
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleAsync<Category>(sqlUpdate, param, unitOfWork.Transaction);
        }

        public Task<Category?> DeleteAsync(long id)
        {
            var param = new { Id = id, DeletedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleOrDefaultAsync<Category?>(sqlDelete, param, unitOfWork.Transaction);
        }

        public Task<IEnumerable<Category>> GetAllInactiveByUserIdAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<Category>(sqlGetAllInactiveByUserId, new { UserId = userId }, unitOfWork.Transaction);
        }

        public Task<Category?> ReactivateAsync(long id)
        {
            var param = new { Id = id, UpdatedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleOrDefaultAsync<Category?>(sqlReactivate, param, unitOfWork.Transaction);
        }
    }
}