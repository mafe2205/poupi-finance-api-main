using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Reports.Entities;
using AppFinancesClassLibrary.Application.Reports.Entities.Results;
using AppFinancesClassLibrary.Domain.Enums;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class ReportsRepository(IUnitOfWork unitOfWork) : IReportsRepository
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        // Movimentação de meta (META_ID preenchido) é transferência, não gasto: fica fora do relatório.
        private const string sqlTransactions = @"
            SELECT t.ID AS TRANSACTION_ID, t.DESCRIPTION, t.CATEGORY_ID, c.NAME AS CATEGORY_NAME, c.TYPE AS CATEGORY_TYPE,
                c.LIMIT_VALUE_CENTS AS CATEGORY_LIMIT_VALUE_CENTS, c.ACTIVE AS CATEGORY_ACTIVE,
                t.PAYMENT_METHOD, t.PAYMENT_VALUE_CENTS, t.TRANSACTION_DATE
            FROM TRANSACTIONS t
            INNER JOIN CATEGORIES c ON c.ID = t.CATEGORY_ID
            WHERE t.USER_ID = :UserId AND t.DELETED_AT IS NULL AND t.META_ID IS NULL
              AND t.TRANSACTION_DATE BETWEEN :From AND :To";

        // A conta que o saldo mexeu é a da transação, ou a do cartão no débito (igual ao TransactionsService).
        // Pagamento de fatura também tira do saldo: entra como uma saída na data do pagamento.
        private const string sqlBalanceMovementsAfter = @"
            SELECT c.TYPE AS CATEGORY_TYPE, t.META_DIRECTION, t.PAYMENT_VALUE_CENTS, t.TRANSACTION_DATE
            FROM TRANSACTIONS t
            INNER JOIN CATEGORIES c ON c.ID = t.CATEGORY_ID
            LEFT JOIN CARDS k ON k.ID = t.CARD_ID
            INNER JOIN BANK_ACCOUNTS b ON b.ID = COALESCE(t.BANK_ACCOUNT_ID, k.BANK_ACCOUNT_ID)
            WHERE t.USER_ID = :UserId AND t.DELETED_AT IS NULL AND t.PAYMENT_METHOD <> :Credito
              AND b.USER_ID = :UserId AND b.ACTIVE
              AND t.TRANSACTION_DATE > :After
            UNION ALL
            SELECT :Saida AS CATEGORY_TYPE, NULL AS META_DIRECTION, p.AMOUNT_CENTS AS PAYMENT_VALUE_CENTS, p.PAYMENT_DATE AS TRANSACTION_DATE
            FROM BANK_STATEMENT_PAYMENTS p
            INNER JOIN BANK_ACCOUNTS b ON b.ID = p.BANK_ACCOUNT_ID
            WHERE b.USER_ID = :UserId AND b.ACTIVE
              AND p.PAYMENT_DATE > :After";

        private const string sqlCurrentBalance = @"
            SELECT COALESCE(SUM(BALANCE_CENTS), 0)
            FROM BANK_ACCOUNTS
            WHERE USER_ID = :UserId AND ACTIVE";

        private const string sqlActiveCards = @"
            SELECT k.ID AS CARD_ID, k.NAME, k.TYPE, k.LIMIT_VALUE_CENTS,
                -- Pagar parte de uma fatura libera essa parte do limite.
                COALESCE((
                    SELECT SUM(t.PAYMENT_VALUE_CENTS)
                    FROM TRANSACTIONS t
                    INNER JOIN BANK_STATEMENTS s ON s.ID = t.BANK_STATEMENT_ID
                    WHERE s.CARD_ID = k.ID AND s.STATUS <> :Paga AND t.DELETED_AT IS NULL
                ), 0) - COALESCE((
                    SELECT SUM(p.AMOUNT_CENTS)
                    FROM BANK_STATEMENT_PAYMENTS p
                    INNER JOIN BANK_STATEMENTS s ON s.ID = p.BANK_STATEMENT_ID
                    WHERE s.CARD_ID = k.ID AND s.STATUS <> :Paga
                ), 0) AS USED_CENTS
            FROM CARDS k
            INNER JOIN BANK_ACCOUNTS b ON b.ID = k.BANK_ACCOUNT_ID
            WHERE b.USER_ID = :UserId AND k.ACTIVE
            ORDER BY k.NAME";

        private const string sqlInvoicesByStatus = @"
            SELECT s.STATUS, COUNT(*) AS COUNT, COALESCE(SUM(totals.TOTAL_CENTS), 0) AS TOTAL_CENTS
            FROM BANK_STATEMENTS s
            INNER JOIN CARDS k ON k.ID = s.CARD_ID
            INNER JOIN BANK_ACCOUNTS b ON b.ID = k.BANK_ACCOUNT_ID
            LEFT JOIN (
                SELECT BANK_STATEMENT_ID, SUM(PAYMENT_VALUE_CENTS) AS TOTAL_CENTS
                FROM TRANSACTIONS
                WHERE USER_ID = :UserId AND DELETED_AT IS NULL AND BANK_STATEMENT_ID IS NOT NULL
                GROUP BY BANK_STATEMENT_ID
            ) totals ON totals.BANK_STATEMENT_ID = s.ID
            WHERE b.USER_ID = :UserId AND s.DUE_DATE BETWEEN :From AND :To
            GROUP BY s.STATUS
            ORDER BY s.STATUS";
        #endregion

        public Task<IEnumerable<ReportTransactionRow>> GetTransactionsAsync(long userId, DateOnly from, DateOnly to)
        {
            var param = new { UserId = userId, From = from, To = to };

            return unitOfWork.Connection.QueryAsync<ReportTransactionRow>(sqlTransactions, param, unitOfWork.Transaction);
        }

        public Task<IEnumerable<ReportBalanceMovementRow>> GetBalanceMovementsAfterAsync(long userId, DateOnly after)
        {
            var param = new { UserId = userId, After = after, Credito = (int)PaymentMethod.cartao_credito, Saida = (int)CategoriesTypes.Saida };

            return unitOfWork.Connection.QueryAsync<ReportBalanceMovementRow>(sqlBalanceMovementsAfter, param, unitOfWork.Transaction);
        }

        public Task<long> GetCurrentBalanceAsync(long userId)
        {
            var param = new { UserId = userId };

            return unitOfWork.Connection.ExecuteScalarAsync<long>(sqlCurrentBalance, param, unitOfWork.Transaction);
        }

        public Task<IEnumerable<ReportCard>> GetActiveCardsAsync(long userId)
        {
            var param = new { UserId = userId, Paga = (int)BankStatementsStatus.paga };

            return unitOfWork.Connection.QueryAsync<ReportCard>(sqlActiveCards, param, unitOfWork.Transaction);
        }

        public Task<IEnumerable<ReportInvoiceGroup>> GetInvoicesByStatusAsync(long userId, DateOnly from, DateOnly to)
        {
            var param = new { UserId = userId, From = from, To = to };

            return unitOfWork.Connection.QueryAsync<ReportInvoiceGroup>(sqlInvoicesByStatus, param, unitOfWork.Transaction);
        }
    }
}
