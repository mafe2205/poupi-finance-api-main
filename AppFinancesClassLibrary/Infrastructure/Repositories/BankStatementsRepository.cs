using AppFinancesClassLibrary.Application.BankStatements.Entities.Dtos;
using AppFinancesClassLibrary.Application.BankStatements.Entities.Results;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class BankStatementsRepository(IUnitOfWork unitOfWork) : IBankStatementsRepository
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetById = @"
            SELECT ID, CARD_ID, MONTH_REFERENCE, CLOSING_DATE, DUE_DATE, PAYMENT_DATE, STATUS, CREATED_AT, UPDATED_AT
            FROM BANK_STATEMENTS
            WHERE ID = :Id";

        private const string sqlGetByCardIdAndMonthReference = @"
            SELECT ID, CARD_ID, MONTH_REFERENCE, CLOSING_DATE, DUE_DATE, PAYMENT_DATE, STATUS, CREATED_AT, UPDATED_AT
            FROM BANK_STATEMENTS
            WHERE CARD_ID = :CardId AND MONTH_REFERENCE = :MonthReference";

        private const string sqlGetByCardId = @"
            SELECT ID, CARD_ID, MONTH_REFERENCE, CLOSING_DATE, DUE_DATE, PAYMENT_DATE, STATUS, CREATED_AT, UPDATED_AT
            FROM BANK_STATEMENTS
            WHERE CARD_ID = :CardId
            ORDER BY MONTH_REFERENCE DESC";

        private const string sqlCreate = @"
            INSERT INTO BANK_STATEMENTS (CARD_ID, MONTH_REFERENCE, CLOSING_DATE, DUE_DATE, STATUS)
            VALUES (:CardId, :MonthReference, :ClosingDate, :DueDate, :Status)
            RETURNING ID, CARD_ID, MONTH_REFERENCE, CLOSING_DATE, DUE_DATE, PAYMENT_DATE, STATUS, CREATED_AT, UPDATED_AT";

        private const string sqlMarkAsPaid = @"
            UPDATE BANK_STATEMENTS SET
                PAYMENT_DATE = :PaymentDate,
                STATUS = :Status,
                UPDATED_AT = :UpdatedAt
            WHERE ID = :Id
            RETURNING ID, CARD_ID, MONTH_REFERENCE, CLOSING_DATE, DUE_DATE, PAYMENT_DATE, STATUS, CREATED_AT, UPDATED_AT";

        // FOR UPDATE trava a fatura até o fim da transação: dois pagamentos ao mesmo tempo não
        // passam os dois pela conferência do que falta pagar.
        private const string sqlGetAmounts = @"
            SELECT
                COALESCE((SELECT SUM(t.PAYMENT_VALUE_CENTS) FROM TRANSACTIONS t
                          WHERE t.BANK_STATEMENT_ID = s.ID AND t.DELETED_AT IS NULL), 0) AS TOTAL_CENTS,
                COALESCE((SELECT SUM(p.AMOUNT_CENTS) FROM BANK_STATEMENT_PAYMENTS p
                          WHERE p.BANK_STATEMENT_ID = s.ID), 0) AS PAID_CENTS
            FROM BANK_STATEMENTS s
            WHERE s.ID = :Id
            FOR UPDATE OF s";

        private const string sqlCreatePayment = @"
            INSERT INTO BANK_STATEMENT_PAYMENTS (BANK_STATEMENT_ID, BANK_ACCOUNT_ID, AMOUNT_CENTS, PAYMENT_DATE, CREATED_AT, UPDATED_AT)
            VALUES (:BankStatementId, :BankAccountId, :AmountCents, :PaymentDate, :CreatedAt, :UpdatedAt)
            RETURNING ID, BANK_STATEMENT_ID, BANK_ACCOUNT_ID, AMOUNT_CENTS, PAYMENT_DATE, CREATED_AT, UPDATED_AT";

        private const string sqlGetPaymentsByStatementId = @"
            SELECT ID, BANK_STATEMENT_ID, BANK_ACCOUNT_ID, AMOUNT_CENTS, PAYMENT_DATE, CREATED_AT, UPDATED_AT
            FROM BANK_STATEMENT_PAYMENTS
            WHERE BANK_STATEMENT_ID = :BankStatementId
            ORDER BY PAYMENT_DATE, ID";
        #endregion

        public Task<BankStatement?> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<BankStatement>(
                sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<BankStatement?> GetByCardIdAndMonthReferenceAsync(long cardId, string monthReference)
        {
            var param = new { CardId = cardId, MonthReference = monthReference };

            return unitOfWork.Connection.QueryFirstOrDefaultAsync<BankStatement>(
                sqlGetByCardIdAndMonthReference,
                param,
                unitOfWork.Transaction);
        }

        public Task<IEnumerable<BankStatement>> GetByCardIdAsync(long cardId)
        {
            return unitOfWork.Connection.QueryAsync<BankStatement>(
                sqlGetByCardId, new { CardId = cardId }, unitOfWork.Transaction);
        }

        public Task<BankStatement> CreateAsync(CreateBankStatementInternalDto dto)
        {
            var param = new
            {
                dto.CardId,
                dto.MonthReference,
                dto.ClosingDate,
                dto.DueDate,
                dto.Status
            };

            return unitOfWork.Connection.QuerySingleAsync<BankStatement>(
                sqlCreate, param, unitOfWork.Transaction);
        }

        public Task<BankStatement> MarkAsPaidAsync(long id, DateOnly paymentDate)
        {
            var param = new
            {
                Id = id,
                PaymentDate = paymentDate,
                Status = BankStatementsStatus.paga,
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleAsync<BankStatement>(sqlMarkAsPaid,
                param,
                unitOfWork.Transaction);
        }

        public async Task<BankStatementAmounts> GetAmountsAsync(long id)
        {
            return await unitOfWork.Connection.QuerySingleAsync<BankStatementAmounts>(
                sqlGetAmounts, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<BankStatementPayment> CreatePaymentAsync(CreateBankStatementPaymentInternalDto dto)
        {
            var now = DateTime.UtcNow;
            var param = new
            {
                dto.BankStatementId,
                dto.BankAccountId,
                dto.AmountCents,
                dto.PaymentDate,
                CreatedAt = now,
                UpdatedAt = now
            };

            return unitOfWork.Connection.QuerySingleAsync<BankStatementPayment>(
                sqlCreatePayment, param, unitOfWork.Transaction);
        }

        public Task<IEnumerable<BankStatementPayment>> GetPaymentsByStatementIdAsync(long bankStatementId)
        {
            return unitOfWork.Connection.QueryAsync<BankStatementPayment>(
                sqlGetPaymentsByStatementId, new { BankStatementId = bankStatementId }, unitOfWork.Transaction);
        }
    }
}
