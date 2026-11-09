using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Transactions.Entities.Dtos;
using AppFinancesClassLibrary.Application.Transactions.Entities.Results;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class TransactionsRepository(IUnitOfWork unitOfWork) : ITransactionsRepository
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetById = @"
            SELECT ID, USER_ID, CATEGORY_ID, META_ID, META_DIRECTION, CARD_ID, BANK_ACCOUNT_ID, BANK_STATEMENT_ID, DESCRIPTION,
                PAYMENT_METHOD, PAYMENT_PARCELS, PAYMENT_VALUE_CENTS, PAYMENT_VALUE_OCR, DESCRIPTION_OCR,
                RECORRENT, RECORRENT_MONTHS, RECORRENT_SERIAL_ID, TRANSACTION_DATE, PAYMENT_DATE, EXPIRING_DATE,
                CATEGORY_CONFIRMED_MANUALLY, ACTIVE, DELETED_AT, CREATED_AT, UPDATED_AT
            FROM TRANSACTIONS
            WHERE ID = :Id AND DELETED_AT IS NULL";

        private const string sqlGetBySerialId = @"
            SELECT ID, USER_ID, CATEGORY_ID, META_ID, META_DIRECTION, CARD_ID, BANK_ACCOUNT_ID, BANK_STATEMENT_ID, DESCRIPTION,
                PAYMENT_METHOD, PAYMENT_PARCELS, PAYMENT_VALUE_CENTS, PAYMENT_VALUE_OCR, DESCRIPTION_OCR,
                RECORRENT, RECORRENT_MONTHS, RECORRENT_SERIAL_ID, TRANSACTION_DATE, PAYMENT_DATE, EXPIRING_DATE,
                CATEGORY_CONFIRMED_MANUALLY, ACTIVE, DELETED_AT, CREATED_AT, UPDATED_AT
            FROM TRANSACTIONS
            WHERE RECORRENT_SERIAL_ID = :RecorrentSerialId AND DELETED_AT IS NULL";

        private const string sqlGetTotalByCategoryAndPeriod = @"
            SELECT COALESCE(SUM(PAYMENT_VALUE_CENTS), 0) FROM TRANSACTIONS
            WHERE USER_ID = :UserId AND CATEGORY_ID = :CategoryId AND DELETED_AT IS NULL
                AND TRANSACTION_DATE >= :From AND TRANSACTION_DATE <= :To";

        private const string sqlGetCategoryTotalsByPeriod = @"
            SELECT CATEGORY_ID AS CategoryId, COALESCE(SUM(PAYMENT_VALUE_CENTS), 0) AS TotalCents
            FROM TRANSACTIONS
            WHERE USER_ID = :UserId AND DELETED_AT IS NULL
                AND TRANSACTION_DATE >= :From AND TRANSACTION_DATE <= :To
            GROUP BY CATEGORY_ID";

        private const string sqlInsert = @"
            INSERT INTO TRANSACTIONS (
                USER_ID, CATEGORY_ID, META_ID, META_DIRECTION, CARD_ID, BANK_ACCOUNT_ID, BANK_STATEMENT_ID, DESCRIPTION,
                PAYMENT_METHOD, PAYMENT_PARCELS, PAYMENT_VALUE_CENTS, PAYMENT_VALUE_OCR, DESCRIPTION_OCR,
                RECORRENT, RECORRENT_MONTHS, RECORRENT_SERIAL_ID, TRANSACTION_DATE, PAYMENT_DATE, EXPIRING_DATE, SOURCE, CATEGORY_CONFIRMED_MANUALLY)
            VALUES (
                :UserId, :CategoryId, :MetaId, :MetaDirection, :CardId, :BankAccountId, :BankStatementId, :Description,
                :PaymentMethod, :PaymentParcels, :PaymentValueCents, :PaymentValueOCR, :DescriptionOCR,
                :Recorrent, :RecorrentValue, :RecorrentSerialId, :TransactionDate, :PaymentDate, :ExpiringDate, :Source, :CategoryConfirmedManually)
            RETURNING ID, USER_ID, CATEGORY_ID, META_ID, META_DIRECTION, CARD_ID, BANK_ACCOUNT_ID, BANK_STATEMENT_ID, DESCRIPTION,
                PAYMENT_METHOD, PAYMENT_PARCELS, PAYMENT_VALUE_CENTS, PAYMENT_VALUE_OCR, DESCRIPTION_OCR,
                RECORRENT, RECORRENT_MONTHS, RECORRENT_SERIAL_ID, TRANSACTION_DATE, PAYMENT_DATE, EXPIRING_DATE,
                CATEGORY_CONFIRMED_MANUALLY, ACTIVE, DELETED_AT, CREATED_AT, UPDATED_AT";

        private const string sqlUpdate = @"
            UPDATE TRANSACTIONS SET
                CATEGORY_ID = :CategoryId,
                META_ID = :MetaId,
                META_DIRECTION = :MetaDirection,
                DESCRIPTION = :Description,
                PAYMENT_VALUE_CENTS = :PaymentValueCents,
                TRANSACTION_DATE = :TransactionDate,
                PAYMENT_DATE = :PaymentDate,
                CATEGORY_CONFIRMED_MANUALLY = :CategoryConfirmedManually,
                UPDATED_AT = :UpdatedAt
            WHERE ID = :Id";

        // Candidatas à reclassificação: categoria vinda do classificador e nunca escolhida pelo
        // usuário. Aportes/retiradas de meta ficam de fora (a categoria Metas é fixa).
        private const string sqlGetAutoClassifiedByUser = @"
            SELECT ID, USER_ID, CATEGORY_ID, META_ID, META_DIRECTION, CARD_ID, BANK_ACCOUNT_ID, BANK_STATEMENT_ID, DESCRIPTION,
                PAYMENT_METHOD, PAYMENT_PARCELS, PAYMENT_VALUE_CENTS, PAYMENT_VALUE_OCR, DESCRIPTION_OCR,
                RECORRENT, RECORRENT_MONTHS, RECORRENT_SERIAL_ID, TRANSACTION_DATE, PAYMENT_DATE, EXPIRING_DATE,
                CATEGORY_CONFIRMED_MANUALLY, ACTIVE, DELETED_AT, CREATED_AT, UPDATED_AT
            FROM TRANSACTIONS
            WHERE USER_ID = :UserId AND DELETED_AT IS NULL AND META_ID IS NULL
                AND CATEGORY_CONFIRMED_MANUALLY = FALSE
            ORDER BY ID";

        // A condição CATEGORY_CONFIRMED_MANUALLY = FALSE repete a do SELECT: se o usuário corrigiu
        // a categoria entre a leitura e a escrita, a reclassificação não sobrescreve.
        private const string sqlUpdateAutoCategory = @"
            UPDATE TRANSACTIONS SET CATEGORY_ID = :CategoryId, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id AND CATEGORY_CONFIRMED_MANUALLY = FALSE";

        private const string sqlSoftDelete = @"
            UPDATE TRANSACTIONS SET ACTIVE = false, DELETED_AT = :DeletedAt, UPDATED_AT = :DeletedAt
            WHERE ID = :Id
            RETURNING DELETED_AT";

        private const string sqlSetPaymentDate = @"
            UPDATE TRANSACTIONS SET PAYMENT_DATE = :PaymentDate, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id";

        private const string sqlGetByUser = @"
                SELECT ID, USER_ID, CATEGORY_ID, META_ID, META_DIRECTION, CARD_ID, BANK_ACCOUNT_ID, BANK_STATEMENT_ID, DESCRIPTION,
                    PAYMENT_METHOD, PAYMENT_PARCELS, PAYMENT_VALUE_CENTS, PAYMENT_VALUE_OCR, DESCRIPTION_OCR,
                    RECORRENT, RECORRENT_MONTHS, RECORRENT_SERIAL_ID, TRANSACTION_DATE, PAYMENT_DATE, EXPIRING_DATE,
                    CATEGORY_CONFIRMED_MANUALLY, ACTIVE, DELETED_AT, CREATED_AT, UPDATED_AT
                FROM TRANSACTIONS
                WHERE USER_ID = :UserId AND DELETED_AT IS NULL
                    -- Filtros opcionais: o cast explícito é necessário porque, quando o valor é
                    -- nulo, o Postgres não consegue deduzir o tipo do parâmetro em ':X IS NULL'.
                    AND (CAST(:StartDate AS DATE) IS NULL OR TRANSACTION_DATE >= CAST(:StartDate AS DATE))
                    AND (CAST(:EndDate AS DATE) IS NULL OR TRANSACTION_DATE <= CAST(:EndDate AS DATE))
                    AND (CAST(:CategoryId AS BIGINT) IS NULL OR CATEGORY_ID = CAST(:CategoryId AS BIGINT))
                    AND (CAST(:CardId AS BIGINT) IS NULL OR CARD_ID = CAST(:CardId AS BIGINT))
                    AND (CAST(:BankAccountId AS BIGINT) IS NULL OR BANK_ACCOUNT_ID = CAST(:BankAccountId AS BIGINT))
                    AND (CAST(:MetaId AS BIGINT) IS NULL OR META_ID = CAST(:MetaId AS BIGINT))
                ORDER BY TRANSACTION_DATE DESC
                LIMIT :PageSize OFFSET :Offset";

        private const string sqlGetTotalByTypeAndPeriod = @"
            SELECT COALESCE(SUM(t.PAYMENT_VALUE_CENTS), 0)
            FROM TRANSACTIONS t
            INNER JOIN CATEGORIES c ON c.ID = t.CATEGORY_ID
            WHERE t.USER_ID = :UserId AND c.TYPE = :Tipo AND t.DELETED_AT IS NULL
            AND t.TRANSACTION_DATE >= :From AND t.TRANSACTION_DATE <= :To";

        private const string sqlCountLate = @"
            SELECT COUNT(*) FROM TRANSACTIONS
            WHERE USER_ID = :UserId AND DELETED_AT IS NULL
                AND PAYMENT_DATE IS NOT NULL AND EXPIRING_DATE IS NOT NULL
                AND PAYMENT_DATE > EXPIRING_DATE
                AND TRANSACTION_DATE >= :From AND TRANSACTION_DATE <= :To";
        #endregion

        public Task<Transaction?> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Transaction>(
                sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<Transaction>> GetBySerialIdAsync(long recorrentSerialId)
        {
            return unitOfWork.Connection.QueryAsync<Transaction>(
                sqlGetBySerialId, new { RecorrentSerialId = recorrentSerialId }, unitOfWork.Transaction);
        }

        public Task<long> NextRecorrentSerialIdAsync()
        {
            return unitOfWork.Connection.ExecuteScalarAsync<long>(
                "SELECT nextval('TRANSACTIONS_RECORRENT_SERIAL_SEQ')", transaction: unitOfWork.Transaction);
        }

        public Task<int> GetTotalByCategoryAndPeriodAsync(GetCategoryTotalsByPeriodDto dto)
        {
            return unitOfWork.Connection.ExecuteScalarAsync<int>(
                sqlGetTotalByCategoryAndPeriod,
                new { UserId = dto.userId, CategoryId = dto.categoryId, From = dto.startDate, To = dto.endDate },
                unitOfWork.Transaction);
        }

        public Task<IEnumerable<CategoryPeriodTotal>> GetCategoryTotalsByPeriodAsync(long userId, DateOnly from, DateOnly to)
        {
            return unitOfWork.Connection.QueryAsync<CategoryPeriodTotal>(
                sqlGetCategoryTotalsByPeriod,
                new { UserId = userId, From = from, To = to },
                unitOfWork.Transaction);
        }

        public Task<IEnumerable<Transaction>> GetByUserAsync(long userId, TransactionFilterDto filter)
        { 

            return unitOfWork.Connection.QueryAsync<Transaction>(sqlGetByUser, new
            {
                UserId = userId,
                filter.StartDate,
                filter.EndDate,
                filter.CategoryId,
                filter.CardId,
                filter.BankAccountId,
                filter.MetaId,
                filter.PageSize,
                Offset = (filter.Page - 1) * filter.PageSize
            }, unitOfWork.Transaction);
        }

        public Task<Transaction> CreateAsync(CreateTransactionInternalDto dto)
        {
            return unitOfWork.Connection.QuerySingleAsync<Transaction>(sqlInsert, dto, unitOfWork.Transaction);
        }

        public Task UpdateAsync(UpdateTransactionInternalDto dto)
        {
            var param = new
            {
                dto.Id,
                dto.CategoryId,
                dto.MetaId,
                dto.MetaDirection,
                dto.Description,
                dto.PaymentValueCents,
                dto.TransactionDate,
                dto.PaymentDate,
                dto.CategoryConfirmedManually,
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.ExecuteAsync(sqlUpdate, param, unitOfWork.Transaction);
        }

        public Task<IEnumerable<Transaction>> GetAutoClassifiedByUserAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<Transaction>(
                sqlGetAutoClassifiedByUser, new { UserId = userId }, unitOfWork.Transaction);
        }

        public async Task<bool> UpdateAutoCategoryAsync(long id, long categoryId)
        {
            var param = new { Id = id, CategoryId = categoryId, UpdatedAt = DateTime.UtcNow };

            var rowsAffected = await unitOfWork.Connection.ExecuteAsync(sqlUpdateAutoCategory, param, unitOfWork.Transaction);
            return rowsAffected > 0;
        }

        public Task<DateTime> SoftDeleteAsync(SoftDeleteDto dto)
        {
            var param = new { dto.Id, DeletedAt = DateTime.UtcNow };

            return unitOfWork.Connection.ExecuteScalarAsync<DateTime>(sqlSoftDelete, param, unitOfWork.Transaction);
        }

        public Task SetPaymentDateAsync(long id, DateOnly paymentDate)
        {
            var param = new { Id = id, PaymentDate = paymentDate, UpdatedAt = DateTime.UtcNow };

            return unitOfWork.Connection.ExecuteAsync(sqlSetPaymentDate, param, unitOfWork.Transaction);
        }

        public Task<int> GetTotalByTypeAndPeriodAsync(long userId, CategoriesTypes tipo, DateOnly from, DateOnly to)
        {
            return unitOfWork.Connection.ExecuteScalarAsync<int>(
                sqlGetTotalByTypeAndPeriod,
                new { UserId = userId, Tipo = tipo, From = from, To = to }, unitOfWork.Transaction);
        }

        public Task<int> CountLateAsync(long userId, DateOnly from, DateOnly to)
        {
            return unitOfWork.Connection.ExecuteScalarAsync<int>(
                sqlCountLate,
                new { UserId = userId, From = from, To = to }, unitOfWork.Transaction);
        }
    }
}