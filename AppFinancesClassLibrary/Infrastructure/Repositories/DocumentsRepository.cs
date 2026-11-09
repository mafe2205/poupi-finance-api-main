using AppFinancesClassLibrary.Application.Documents.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class DocumentsRepository(IUnitOfWork unitOfWork) : IDocumentsRepository
    {
        private readonly IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetById = @"
            SELECT ID, USER_ID, TRANSACTION_ID, DOCUMENT_URL, TEXT_OCR, TRUST_OCR, VALUE_TRUST_OCR, STATUS, CONFIRMED_MANUALLY, IDEMPOTENCY_KEY, ACTIVE, DELETED_AT, OCR_STARTED_AT, OCR_FINISHED_AT, ESTABLISHMENT_CNAE, ESTABLISHMENT_REGISTRY_NAME, CREATED_AT, UPDATED_AT
            FROM DOCUMENTS
            WHERE ID = :Id AND ACTIVE";

        private const string sqlGetAllByUserId = @"
            SELECT ID, USER_ID, TRANSACTION_ID, DOCUMENT_URL, TEXT_OCR, TRUST_OCR, VALUE_TRUST_OCR, STATUS, CONFIRMED_MANUALLY, IDEMPOTENCY_KEY, ACTIVE, DELETED_AT, OCR_STARTED_AT, OCR_FINISHED_AT, ESTABLISHMENT_CNAE, ESTABLISHMENT_REGISTRY_NAME, CREATED_AT, UPDATED_AT
            FROM DOCUMENTS
            WHERE USER_ID = :UserId AND ACTIVE
            ORDER BY CREATED_AT DESC";

        private const string sqlInsert = @"
            INSERT INTO DOCUMENTS (USER_ID, TRANSACTION_ID, DOCUMENT_URL, TEXT_OCR, TRUST_OCR, STATUS, CONFIRMED_MANUALLY, IDEMPOTENCY_KEY, CREATED_AT, UPDATED_AT)
            VALUES (:UserId, :TransactionId, :DocumentUrl, :TextOCR, :TrustOCR, :Status, :ConfirmedManually, :IdempotencyKey, :CreatedAt, :UpdatedAt)
            -- Mesma chave de idempotência já usada pelo usuário: não insere e não retorna linha.
            ON CONFLICT (USER_ID, IDEMPOTENCY_KEY) WHERE IDEMPOTENCY_KEY IS NOT NULL AND ACTIVE DO NOTHING
            RETURNING ID, USER_ID, TRANSACTION_ID, DOCUMENT_URL, TEXT_OCR, TRUST_OCR, VALUE_TRUST_OCR, STATUS, CONFIRMED_MANUALLY, IDEMPOTENCY_KEY, ACTIVE, DELETED_AT, OCR_STARTED_AT, OCR_FINISHED_AT, ESTABLISHMENT_CNAE, ESTABLISHMENT_REGISTRY_NAME, CREATED_AT, UPDATED_AT";

        private const string sqlUpdateTransaction = @"
            UPDATE DOCUMENTS SET
                TRANSACTION_ID = :TransactionId, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id AND ACTIVE
            RETURNING ID, USER_ID, TRANSACTION_ID, DOCUMENT_URL, TEXT_OCR, TRUST_OCR, VALUE_TRUST_OCR, STATUS, CONFIRMED_MANUALLY, IDEMPOTENCY_KEY, ACTIVE, DELETED_AT, OCR_STARTED_AT, OCR_FINISHED_AT, ESTABLISHMENT_CNAE, ESTABLISHMENT_REGISTRY_NAME, CREATED_AT, UPDATED_AT";

        private const string sqlUpdateStatus = @"
            UPDATE DOCUMENTS SET
                STATUS = :Status, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id
            RETURNING ID, USER_ID, TRANSACTION_ID, DOCUMENT_URL, TEXT_OCR, TRUST_OCR, VALUE_TRUST_OCR, STATUS, CONFIRMED_MANUALLY, IDEMPOTENCY_KEY, ACTIVE, DELETED_AT, OCR_STARTED_AT, OCR_FINISHED_AT, ESTABLISHMENT_CNAE, ESTABLISHMENT_REGISTRY_NAME, CREATED_AT, UPDATED_AT";

        private const string sqlUpdateDocumentUrl = @"
            UPDATE DOCUMENTS SET
                DOCUMENT_URL = :DocumentUrl, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id
            RETURNING ID, USER_ID, TRANSACTION_ID, DOCUMENT_URL, TEXT_OCR, TRUST_OCR, VALUE_TRUST_OCR, STATUS, CONFIRMED_MANUALLY, IDEMPOTENCY_KEY, ACTIVE, DELETED_AT, OCR_STARTED_AT, OCR_FINISHED_AT, ESTABLISHMENT_CNAE, ESTABLISHMENT_REGISTRY_NAME, CREATED_AT, UPDATED_AT";

        private const string sqlUpdateOcrResult = @"
            UPDATE DOCUMENTS SET
                TEXT_OCR = :TextOCR, TRUST_OCR = :TrustOCR, VALUE_TRUST_OCR = :ValueTrustOcr, OCR_FINISHED_AT = :OcrFinishedAt, ESTABLISHMENT_CNAE = :EstablishmentCnae, ESTABLISHMENT_REGISTRY_NAME = :EstablishmentRegistryName, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id
            RETURNING ID, USER_ID, TRANSACTION_ID, DOCUMENT_URL, TEXT_OCR, TRUST_OCR, VALUE_TRUST_OCR, STATUS, CONFIRMED_MANUALLY, IDEMPOTENCY_KEY, ACTIVE, DELETED_AT, OCR_STARTED_AT, OCR_FINISHED_AT, ESTABLISHMENT_CNAE, ESTABLISHMENT_REGISTRY_NAME, CREATED_AT, UPDATED_AT";

        private const string sqlUpdateConfirmedManually = @"
            UPDATE DOCUMENTS SET
                CONFIRMED_MANUALLY = :ConfirmedManually, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id AND ACTIVE
            RETURNING ID, USER_ID, TRANSACTION_ID, DOCUMENT_URL, TEXT_OCR, TRUST_OCR, VALUE_TRUST_OCR, STATUS, CONFIRMED_MANUALLY, IDEMPOTENCY_KEY, ACTIVE, DELETED_AT, OCR_STARTED_AT, OCR_FINISHED_AT, ESTABLISHMENT_CNAE, ESTABLISHMENT_REGISTRY_NAME, CREATED_AT, UPDATED_AT";

        private const string sqlGetByIdempotencyKey = @"
            SELECT ID, USER_ID, TRANSACTION_ID, DOCUMENT_URL, TEXT_OCR, TRUST_OCR, VALUE_TRUST_OCR, STATUS, CONFIRMED_MANUALLY, IDEMPOTENCY_KEY, ACTIVE, DELETED_AT, OCR_STARTED_AT, OCR_FINISHED_AT, ESTABLISHMENT_CNAE, ESTABLISHMENT_REGISTRY_NAME, CREATED_AT, UPDATED_AT
            FROM DOCUMENTS
            WHERE USER_ID = :UserId AND IDEMPOTENCY_KEY = :IdempotencyKey AND ACTIVE";

        // Só vincula se o documento ainda não tiver transação: duas confirmações simultâneas
        // do mesmo documento não criam duas transações (a segunda não atualiza nenhuma linha).
        private const string sqlLinkTransactionIfUnlinked = @"
            UPDATE DOCUMENTS SET
                TRANSACTION_ID = :TransactionId, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id AND TRANSACTION_ID IS NULL AND ACTIVE";

        // Soft delete: o texto do OCR, os itens e a imagem continuam guardados.
        private const string sqlDelete = @"
            UPDATE DOCUMENTS SET ACTIVE = false, DELETED_AT = :DeletedAt, UPDATED_AT = :DeletedAt
            WHERE ID = :Id AND ACTIVE";

        private const string sqlMarkOcrStarted = "UPDATE DOCUMENTS SET OCR_STARTED_AT = :OcrStartedAt WHERE ID = :Id";
        #endregion

        public Task<Document> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Document>(sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task MarkOcrStartedAsync(long id, DateTime ocrStartedAt)
        {
            return unitOfWork.Connection.ExecuteAsync(sqlMarkOcrStarted, new { Id = id, OcrStartedAt = ocrStartedAt }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<Document>> GetAllByUserIdAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<Document>(sqlGetAllByUserId, new { UserId = userId }, unitOfWork.Transaction);
        }

        public Task<Document?> CreateAsync(CreateDocumentDto dto)
        {
            var now = DateTime.UtcNow;
            var param = new
            {
                dto.UserId,
                dto.TransactionId,
                dto.DocumentUrl,
                dto.TextOCR,
                dto.TrustOCR,
                dto.Status,
                dto.ConfirmedManually,
                dto.IdempotencyKey,
                CreatedAt = now,
                UpdatedAt = now
            };

            return unitOfWork.Connection.QuerySingleOrDefaultAsync<Document?>(sqlInsert, param, unitOfWork.Transaction);
        }

        public Task<Document> UpdateTransactionAsync(LinkTransactionDto dto)
        {
            var param = new { dto.Id, dto.TransactionId, UpdatedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleAsync<Document>(sqlUpdateTransaction, param, unitOfWork.Transaction);
        }

        public Task<Document?> GetByIdempotencyKeyAsync(GetByIdempotencyKeyDto dto)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Document?>(sqlGetByIdempotencyKey, dto, unitOfWork.Transaction);
        }

        public async Task<bool> TryLinkTransactionAsync(LinkTransactionDto dto)
        {
            var param = new { dto.Id, dto.TransactionId, UpdatedAt = DateTime.UtcNow };

            var rowsAffected = await unitOfWork.Connection.ExecuteAsync(sqlLinkTransactionIfUnlinked, param, unitOfWork.Transaction);
            return rowsAffected > 0;
        }

        public Task<Document> UpdateStatusAsync(UpdateStatusDto dto)
        {
            var param = new { dto.Id, dto.Status, UpdatedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleAsync<Document>(sqlUpdateStatus, param, unitOfWork.Transaction);
        }

        public Task<Document> UpdateDocumentUrlAsync(UpdateDocumentUrlDto dto)
        {
            var param = new { dto.Id, dto.DocumentUrl, UpdatedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleAsync<Document>(sqlUpdateDocumentUrl, param, unitOfWork.Transaction);
        }

        public Task<Document> UpdateOcrResultAsync(UpdateOcrResultDto dto)
        {
            var param = new
            {
                dto.Id,
                dto.TextOcr,
                dto.TrustOcr,
                dto.ValueTrustOcr,
                dto.OcrFinishedAt,
                dto.EstablishmentCnae,
                dto.EstablishmentRegistryName,
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleAsync<Document>(sqlUpdateOcrResult, param, unitOfWork.Transaction);
        }

        public Task<Document> UpdateConfirmedManuallyAsync(ConfirmManuallyDto dto)
        {
            var param = new { dto.Id, dto.ConfirmedManually, UpdatedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleAsync<Document>(sqlUpdateConfirmedManually, param, unitOfWork.Transaction);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var param = new { Id = id, DeletedAt = DateTime.UtcNow };

            var rowsAffected = await unitOfWork.Connection.ExecuteAsync(sqlDelete, param, unitOfWork.Transaction);
            return rowsAffected > 0;
        }
    }
}