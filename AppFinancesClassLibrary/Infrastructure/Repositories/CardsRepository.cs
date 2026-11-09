using AppFinancesClassLibrary.Application.Cards.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class CardsRepository(IUnitOfWork unitOfWork) : ICardsRepository
    {
        private IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetById = @"
            SELECT ID, BANK_ACCOUNT_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CLOSING_DATE, DUE_DATE, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM CARDS
            WHERE ID = :Id AND ACTIVE";

        private const string sqlGetByIdIncludingInactive = @"
            SELECT ID, BANK_ACCOUNT_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CLOSING_DATE, DUE_DATE, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM CARDS
            WHERE ID = :Id";

        private const string sqlGetAllByBankAccountId = @"
            SELECT ID, BANK_ACCOUNT_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CLOSING_DATE, DUE_DATE, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM CARDS
            WHERE BANK_ACCOUNT_ID = :BankAccountId AND ACTIVE
            ORDER BY NAME";

        private const string sqlInsert = @"
            INSERT INTO CARDS (BANK_ACCOUNT_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CLOSING_DATE, DUE_DATE, CREATED_AT)
            VALUES (:BankAccountId, :Name, :Type, :LimitValueCents, :ClosingDate, :DueDate, :CreatedAt)
            RETURNING ID, BANK_ACCOUNT_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CLOSING_DATE, DUE_DATE, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        private const string sqlUpdate = @"
            UPDATE CARDS SET
                NAME = COALESCE(:Name, NAME),
                TYPE = COALESCE(:Type, TYPE),
                LIMIT_VALUE_CENTS = CASE WHEN :LimitValueCents = 0 THEN LIMIT_VALUE_CENTS ELSE :LimitValueCents END,
                CLOSING_DATE = CASE WHEN :ClosingDate = 0 THEN CLOSING_DATE ELSE :ClosingDate END,
                DUE_DATE = CASE WHEN :DueDate = 0 THEN DUE_DATE ELSE :DueDate END,
                UPDATED_AT = :UpdatedAt
            WHERE ID = :Id AND ACTIVE
            RETURNING ID, BANK_ACCOUNT_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CLOSING_DATE, DUE_DATE, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        private const string sqlDelete = @"
            UPDATE CARDS SET ACTIVE = false, DELETED_AT = :DeletedAt, UPDATED_AT = :DeletedAt
            WHERE ID = :Id AND ACTIVE
            RETURNING ID, BANK_ACCOUNT_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CLOSING_DATE, DUE_DATE, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        // Cartão não tem USER_ID: o dono vem da conta bancária.
        private const string sqlGetAllInactiveByUserId = @"
            SELECT c.ID, c.BANK_ACCOUNT_ID, c.NAME, c.TYPE, c.LIMIT_VALUE_CENTS, c.CLOSING_DATE, c.DUE_DATE, c.CREATED_AT, c.UPDATED_AT, c.ACTIVE, c.DELETED_AT
            FROM CARDS c
            JOIN BANK_ACCOUNTS b ON b.ID = c.BANK_ACCOUNT_ID
            WHERE b.USER_ID = :UserId AND NOT c.ACTIVE
            ORDER BY c.NAME";

        private const string sqlReactivate = @"
            UPDATE CARDS SET ACTIVE = true, DELETED_AT = NULL, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id AND NOT ACTIVE
            RETURNING ID, BANK_ACCOUNT_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CLOSING_DATE, DUE_DATE, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";
        #endregion

        public Task<Card> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Card>(sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<Card> GetByIdIncludingInactiveAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Card>(sqlGetByIdIncludingInactive, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<Card>> GetAllByBankAccountIdAsync(long bankAccountId)
        {
            return unitOfWork.Connection.QueryAsync<Card>(sqlGetAllByBankAccountId, new { BankAccountId = bankAccountId }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<Card>> GetAllInactiveByUserIdAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<Card>(sqlGetAllInactiveByUserId, new { UserId = userId }, unitOfWork.Transaction);
        }

        public Task<Card> ReactivateAsync(long id)
        {
            var param = new { Id = id, UpdatedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleOrDefaultAsync<Card>(sqlReactivate, param, unitOfWork.Transaction);
        }

        public Task<Card> CreateAsync(CreateCardDto dto)
        {
            var param = new
            {
                dto.BankAccountId,
                dto.Name,
                dto.Type,
                dto.LimitValueCents,
                dto.ClosingDate,
                dto.DueDate,
                CreatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleAsync<Card>(sqlInsert, param, unitOfWork.Transaction);
        }

        public Task<Card> UpdateAsync(UpdateCardDto dto)
        {
            var param = new
            {
                dto.Id,
                dto.Name,
                dto.Type,
                dto.LimitValueCents,
                dto.ClosingDate,
                dto.DueDate,
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleAsync<Card>(sqlUpdate, param, unitOfWork.Transaction);
        }

        public Task<Card> DeleteAsync(long id)
        {
            var param = new { Id = id, DeletedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleOrDefaultAsync<Card>(sqlDelete, param, unitOfWork.Transaction);
        }
    }
}