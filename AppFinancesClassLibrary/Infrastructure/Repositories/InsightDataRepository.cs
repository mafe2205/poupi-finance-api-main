using AppFinancesClassLibrary.Application.BehavioralInsights.Rules;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Enums;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class InsightDataRepository(IUnitOfWork unitOfWork) : IInsightDataRepository
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlCategories = @"
            SELECT ID, NAME, TYPE, IS_DEFAULT, LIMIT_VALUE_CENTS, ACTIVE
            FROM CATEGORIES
            WHERE USER_ID = :UserId";

        private const string sqlTransactions = @"
            SELECT ID, CATEGORY_ID, META_ID, META_DIRECTION, CARD_ID, BANK_ACCOUNT_ID, BANK_STATEMENT_ID, DESCRIPTION,
                PAYMENT_METHOD, PAYMENT_PARCELS, PAYMENT_VALUE_CENTS, TRANSACTION_DATE, CREATED_AT
            FROM TRANSACTIONS
            WHERE USER_ID = :UserId AND DELETED_AT IS NULL AND TRANSACTION_DATE >= :From";

        private const string sqlTransactionSummary = @"
            SELECT MIN(TRANSACTION_DATE) AS FIRST_TRANSACTION_DATE, MAX(CREATED_AT) AS LAST_REGISTERED_AT
            FROM TRANSACTIONS
            WHERE USER_ID = :UserId AND DELETED_AT IS NULL";

        // Progresso = aportes - retiradas já realizados (META_DIRECTION: 0 = deposito, 1 = retirada).
        private const string sqlGoals = @"
            SELECT g.ID, g.DESCRIPTION, g.INICIAL_DATE, g.FINAL_DATE, g.INICIAL_VALUE_CENTS, g.FINAL_VALUE_CENTS,
                COALESCE(SUM(CASE WHEN t.META_DIRECTION = 1 THEN -t.PAYMENT_VALUE_CENTS ELSE t.PAYMENT_VALUE_CENTS END), 0) AS CONTRIBUTED_CENTS,
                MAX(CASE WHEN t.META_DIRECTION = 0 THEN t.TRANSACTION_DATE END) AS LAST_DEPOSIT_DATE
            FROM GOALS g
            LEFT JOIN TRANSACTIONS t ON t.META_ID = g.ID AND t.DELETED_AT IS NULL AND t.TRANSACTION_DATE <= :Today
            WHERE g.USER_ID = :UserId AND g.ACTIVE
            GROUP BY g.ID, g.DESCRIPTION, g.INICIAL_DATE, g.FINAL_DATE, g.INICIAL_VALUE_CENTS, g.FINAL_VALUE_CENTS";

        // Inclui cartões inativos: transações antigas ainda apontam para eles (saldo da conta do débito).
        private const string sqlCards = @"
            SELECT c.ID, c.BANK_ACCOUNT_ID, c.NAME, c.TYPE, c.LIMIT_VALUE_CENTS, c.CLOSING_DATE, c.DUE_DATE, c.CREATED_AT, c.ACTIVE,
                (SELECT MAX(t.TRANSACTION_DATE) FROM TRANSACTIONS t
                 WHERE t.CARD_ID = c.ID AND t.DELETED_AT IS NULL AND t.TRANSACTION_DATE <= :Today) AS LAST_TRANSACTION_DATE
            FROM CARDS c
            INNER JOIN BANK_ACCOUNTS b ON b.ID = c.BANK_ACCOUNT_ID
            WHERE b.USER_ID = :UserId";

        private const string sqlStatements = @"
            SELECT s.ID, s.CARD_ID, s.MONTH_REFERENCE, s.CLOSING_DATE, s.DUE_DATE, s.STATUS,
                COALESCE((SELECT SUM(p.AMOUNT_CENTS) FROM BANK_STATEMENT_PAYMENTS p WHERE p.BANK_STATEMENT_ID = s.ID), 0) AS PAID_CENTS
            FROM BANK_STATEMENTS s
            INNER JOIN CARDS c ON c.ID = s.CARD_ID
            INNER JOIN BANK_ACCOUNTS b ON b.ID = c.BANK_ACCOUNT_ID
            WHERE b.USER_ID = :UserId AND s.CLOSING_DATE >= :From";

        // Agrupa pela fatura, não pela data: parcelas de compras antigas caem em faturas recentes.
        private const string sqlStatementTotals = @"
            SELECT t.BANK_STATEMENT_ID AS STATEMENT_ID, t.CATEGORY_ID, SUM(t.PAYMENT_VALUE_CENTS) AS TOTAL_CENTS, COUNT(*) AS TRANSACTION_COUNT
            FROM TRANSACTIONS t
            INNER JOIN BANK_STATEMENTS s ON s.ID = t.BANK_STATEMENT_ID
            WHERE t.USER_ID = :UserId AND t.DELETED_AT IS NULL AND s.CLOSING_DATE >= :From
            GROUP BY t.BANK_STATEMENT_ID, t.CATEGORY_ID";

        private const string sqlAccounts = @"
            SELECT ID, BANK_NAME, IS_DEFAULT, BALANCE_CENTS, CREATED_AT
            FROM BANK_ACCOUNTS
            WHERE USER_ID = :UserId AND ACTIVE";

        // Lido pelo OCR e ainda sem transação: está esperando a revisão do usuário.
        private const string sqlPendingDocuments = @"
            SELECT ID, CREATED_AT, TEXT_OCR
            FROM DOCUMENTS
            WHERE USER_ID = :UserId AND STATUS = :Processado AND TRANSACTION_ID IS NULL AND ACTIVE";

        // A descrição da transação só é necessária para a deduplicação da R20 (gasto recorrente).
        private const string sqlHistory = @"
            SELECT bi.RULE_CODE, bi.TYPE, bi.CATEGORY_ID, bi.RELATED_ENTITY_ID, bi.REFERENCE_DATE, bi.CREATED_AT,
                t.DESCRIPTION AS RELATED_DESCRIPTION
            FROM BEHAVIORAL_INSIGHTS bi
            LEFT JOIN TRANSACTIONS t ON bi.RULE_CODE = :RecurringRuleCode AND t.ID = bi.RELATED_ENTITY_ID
            -- Desatualizados não contam: o insight que os substitui pode reusar regra/entidade/período.
            WHERE bi.USER_ID = :UserId AND bi.OUTDATED_AT IS NULL";
        #endregion

        public async Task<InsightUserData> LoadAsync(long userId, DateOnly today, DateOnly transactionsFrom)
        {
            var connection = unitOfWork.Connection;
            var transaction = unitOfWork.Transaction;
            var byUser = new { UserId = userId, Today = today, From = transactionsFrom };

            var summary = await connection.QuerySingleAsync<TransactionSummary>(sqlTransactionSummary, byUser, transaction);

            var statements = (await connection.QueryAsync<InsightStatement>(sqlStatements, byUser, transaction)).ToList();
            var totals = (await connection.QueryAsync<StatementCategoryTotal>(sqlStatementTotals, byUser, transaction)).ToList();
            foreach (var statement in statements)
            {
                var ofStatement = totals.Where(t => t.StatementId == statement.Id).ToList();
                statement.TotalCents = ofStatement.Sum(t => t.TotalCents);
                statement.TransactionCount = (int)ofStatement.Sum(t => t.TransactionCount);
                statement.CategoryTotals = ofStatement.ToDictionary(t => t.CategoryId, t => t.TotalCents);
            }

            var history = (await connection.QueryAsync<InsightHistoryEntry>(sqlHistory,
                new { UserId = userId, RecurringRuleCode = InsightRuleCodes.GastoRecorrente }, transaction)).ToList();

            foreach (var entry in history.Where(h => h.RuleCode is null && h.Type == BehavioralInsightTypes.sugestao))
                entry.RuleCode = InsightRuleCodes.AumentoSemanal;

            return new InsightUserData
            {
                UserId = userId,
                Categories = (await connection.QueryAsync<InsightCategory>(sqlCategories, byUser, transaction)).ToList(),
                Transactions = (await connection.QueryAsync<InsightTransaction>(sqlTransactions, byUser, transaction)).ToList(),
                Goals = (await connection.QueryAsync<InsightGoal>(sqlGoals, byUser, transaction)).ToList(),
                Cards = (await connection.QueryAsync<InsightCard>(sqlCards, byUser, transaction)).ToList(),
                Statements = statements,
                Accounts = (await connection.QueryAsync<InsightAccount>(sqlAccounts, byUser, transaction)).ToList(),
                PendingDocuments = (await connection.QueryAsync<InsightPendingDocument>(sqlPendingDocuments,
                    new { UserId = userId, Processado = StatusOCRTypes.processado }, transaction)).ToList(),
                History = history,
                FirstTransactionDate = summary.FirstTransactionDate,
                LastRegisteredAt = summary.LastRegisteredAt
            };
        }

        private sealed class TransactionSummary
        {
            public DateOnly? FirstTransactionDate { get; set; }
            public DateTime? LastRegisteredAt { get; set; }
        }

        private sealed class StatementCategoryTotal
        {
            public long StatementId { get; set; }
            public long CategoryId { get; set; }
            public long TotalCents { get; set; }
            public long TransactionCount { get; set; }
        }
    }
}
