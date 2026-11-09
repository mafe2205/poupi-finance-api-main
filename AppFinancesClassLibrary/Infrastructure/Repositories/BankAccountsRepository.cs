using AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class BankAccountsRepository(IUnitOfWork unitOfWork) : IBankAccountsRepository
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetById = @"
            SELECT ID, USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, BALANCE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM BANK_ACCOUNTS
            WHERE ID = @Id AND ACTIVE";

        private const string sqlGetByIdIncludingInactive = @"
            SELECT ID, USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, BALANCE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM BANK_ACCOUNTS
            WHERE ID = @Id";

        private const string sqlGetAllInactiveByUserId = @"
            SELECT ID, USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, BALANCE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM BANK_ACCOUNTS
            WHERE USER_ID = @UserId AND NOT ACTIVE
            ORDER BY BANK_NAME";

        private const string sqlGetAllByUserId = @"
            SELECT ID, USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, BALANCE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT
            FROM BANK_ACCOUNTS
            WHERE USER_ID = @UserId AND ACTIVE
            ORDER BY BANK_NAME";

        private const string sqlInsert = @"
            INSERT INTO BANK_ACCOUNTS (USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, BALANCE_CENTS, CREATED_AT, UPDATED_AT)
            VALUES (@UserId, @BankName, @Agency, @AccountNumber, @IsDefault, @BalanceCents, @CreatedAt, @UpdatedAt)
            RETURNING ID, USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, BALANCE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        private const string sqlUpdate = @"
            UPDATE BANK_ACCOUNTS SET
                BANK_NAME = COALESCE(@BankName, BANK_NAME),
                AGENCY = COALESCE(@Agency, AGENCY),
                ACCOUNT_NUMBER = COALESCE(@AccountNumber, ACCOUNT_NUMBER),
                UPDATED_AT = @UpdatedAt
            WHERE ID = @Id AND ACTIVE
            RETURNING ID, USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, BALANCE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        private const string sqlSetBalance = @"
            UPDATE BANK_ACCOUNTS SET BALANCE_CENTS = @BalanceCents, UPDATED_AT = @UpdatedAt
            WHERE ID = @Id AND ACTIVE
            RETURNING ID, USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, BALANCE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        // Sem filtro de ACTIVE: excluir ou editar uma transação antiga ainda precisa estornar
        // o saldo de uma conta que já foi desativada.
        private const string sqlAdjustBalance = @"
            UPDATE BANK_ACCOUNTS SET BALANCE_CENTS = BALANCE_CENTS + @Delta, UPDATED_AT = @UpdatedAt
            WHERE ID = @Id
            RETURNING ID, USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, BALANCE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        // Exclusão lógica: a conta e os cartões dela são desativados, mas continuam no banco
        // porque as transações antigas apontam para eles.
        private const string sqlDelete = @"
            WITH deactivated_cards AS (
                UPDATE CARDS SET ACTIVE = false, DELETED_AT = @DeletedAt, UPDATED_AT = @DeletedAt
                WHERE BANK_ACCOUNT_ID = @Id AND ACTIVE
            )
            UPDATE BANK_ACCOUNTS SET ACTIVE = false, DELETED_AT = @DeletedAt, UPDATED_AT = @DeletedAt
            WHERE ID = @Id AND ACTIVE
            RETURNING ID, USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, BALANCE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";

        // Reativa junto só os cartões desativados em cascata com a conta, que são os que têm
        // o mesmo DELETED_AT dela. Os desativados antes, um a um, continuam desativados.
        // As duas partes leem a mesma foto do banco, então o DELETED_AT lido ainda é o antigo.
        private const string sqlReactivate = @"
            WITH reactivated_cards AS (
                UPDATE CARDS SET ACTIVE = true, DELETED_AT = NULL, UPDATED_AT = @UpdatedAt
                WHERE BANK_ACCOUNT_ID = @Id AND NOT ACTIVE
                  AND DELETED_AT = (SELECT DELETED_AT FROM BANK_ACCOUNTS WHERE ID = @Id AND NOT ACTIVE)
            )
            UPDATE BANK_ACCOUNTS SET ACTIVE = true, DELETED_AT = NULL, UPDATED_AT = @UpdatedAt
            WHERE ID = @Id AND NOT ACTIVE
            RETURNING ID, USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, BALANCE_CENTS, CREATED_AT, UPDATED_AT, ACTIVE, DELETED_AT";
        #endregion

        public Task<BankAccount?> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<BankAccount?>(sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<BankAccount?> GetByIdIncludingInactiveAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<BankAccount?>(sqlGetByIdIncludingInactive, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<BankAccount>> GetAllInactiveByUserIdAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<BankAccount>(sqlGetAllInactiveByUserId, new { UserId = userId }, unitOfWork.Transaction);
        }

        public Task<BankAccount?> ReactivateAsync(long id)
        {
            var param = new { Id = id, UpdatedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleOrDefaultAsync<BankAccount?>(sqlReactivate, param, unitOfWork.Transaction);
        }

        public Task<IEnumerable<BankAccount>> GetAllByUserIdAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<BankAccount>(sqlGetAllByUserId, new { UserId = userId }, unitOfWork.Transaction);
        }

        public Task<BankAccount> CreateAsync(CreateBankAccountDto dto)
        {
            var now = DateTime.UtcNow;
            var param = new
            {
                dto.UserId,
                dto.BankName,
                dto.Agency,
                dto.AccountNumber,
                dto.IsDefault,
                dto.BalanceCents,
                CreatedAt = now,
                UpdatedAt = now
            };

            return unitOfWork.Connection.QuerySingleAsync<BankAccount>(sqlInsert, param, unitOfWork.Transaction);
        }

        public async Task<BankAccount> UpdateAsync(UpdateBankAccountDto dto)
        {
            var param = new
            {
                dto.Id,
                dto.BankName,
                dto.Agency,
                dto.AccountNumber,
                UpdatedAt = DateTime.UtcNow
            };

            return await unitOfWork.Connection.QuerySingleOrDefaultAsync<BankAccount>(sqlUpdate, param, unitOfWork.Transaction);
        }

        public async Task<BankAccount> SetBalanceAsync(long id, int balanceCents)
        {
            var param = new { Id = id, BalanceCents = balanceCents, UpdatedAt = DateTime.UtcNow };

            return await unitOfWork.Connection.QuerySingleOrDefaultAsync<BankAccount>(sqlSetBalance, param, unitOfWork.Transaction);
        }

        public async Task<BankAccount> AdjustBalanceAsync(long id, int deltaCents)
        {
            var param = new { Id = id, Delta = deltaCents, UpdatedAt = DateTime.UtcNow };

            return await unitOfWork.Connection.QuerySingleOrDefaultAsync<BankAccount>(sqlAdjustBalance, param, unitOfWork.Transaction);
        }

        public Task<BankAccount?> DeleteAsync(long id)
        {
            var param = new { Id = id, DeletedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleOrDefaultAsync<BankAccount?>(sqlDelete, param, unitOfWork.Transaction);
        }
    }
}