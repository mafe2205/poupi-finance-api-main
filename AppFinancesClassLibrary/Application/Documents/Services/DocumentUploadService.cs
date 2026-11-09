using AppFinancesClassLibrary.Application.Documents.Entities.Dtos;
using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.User.Entities.Results;
using AppFinancesClassLibrary.Application.User.Services;
using AppFinancesClassLibrary.Domain.Enums;
using AppFinancesClassLibrary.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AppFinancesClassLibrary.Application.Documents.Services
{
    public class DocumentUploadService(
        IDocumentsRepository documentsRepository,
        IDocumentItemsRepository documentItemsRepository,
        IDocumentParsingService documentParsingService,
        IUnitOfWork unitOfWork,
        IOcrService ocrService,
        IDocumentFileStorage fileStorage,
        IBackgroundTaskQueue backgroundTaskQueue,
        ILogger<DocumentUploadService> logger,
        IUserService userService,
        ICnpjActivityService cnpjActivityService,
        ICurrentUser currentUser) : IDocumentUploadService
    {
        private readonly IDocumentsRepository documentsRepository = documentsRepository;
        private readonly IDocumentItemsRepository documentItemsRepository = documentItemsRepository;
        private readonly IDocumentParsingService documentParsingService = documentParsingService;
        private readonly IUnitOfWork unitOfWork = unitOfWork;
        private readonly IOcrService ocrService = ocrService;
        private readonly IDocumentFileStorage fileStorage = fileStorage;
        private readonly IBackgroundTaskQueue backgroundTaskQueue = backgroundTaskQueue;
        private readonly ILogger<DocumentUploadService> logger = logger;
        private readonly IUserService userService = userService;
        private readonly ICnpjActivityService cnpjActivityService = cnpjActivityService;

        private const int MaxIdempotencyKeyLength = 100;

        public async Task<DocumentUploadResult> UploadAsync(Stream imageStream, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            long userId = currentUser.UserId;

            if (imageStream is null)
                throw new DomainValidationException("Arquivo de imagem é obrigatório.");

            UserResult userFound = await userService.GetByIdAsync(userId)
                ?? throw new DomainValidationException($"Usuário {userId} não encontrado.");

            idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
            if (idempotencyKey is not null)
            {
                Domain.Validate.String(idempotencyKey, nameof(idempotencyKey), minLength: 1, maxLength: MaxIdempotencyKeyLength);

                GetByIdempotencyKeyDto getByIdempotencyKeyDto = new() 
                { 
                    UserId = userId,
                    IdempotencyKey = idempotencyKey 
                };

                var existing = await documentsRepository.GetByIdempotencyKeyAsync(getByIdempotencyKeyDto);
                if (existing is not null) return new DocumentUploadResult(existing.Id, Created: false);
            }

            using var buffer = new MemoryStream();
            await imageStream.CopyToAsync(buffer, cancellationToken);
            var imageBytes = buffer.ToArray();

            if (imageBytes.Length == 0)
                throw new DomainValidationException("Arquivo de imagem vazio.");

            CreateDocumentDto createDocumentDto = new()
            {
                UserId = userId,
                TransactionId = null,
                DocumentUrl = "pending",
                TextOCR = string.Empty,
                TrustOCR = 0,
                Status = StatusOCRTypes.pendente,
                IdempotencyKey = idempotencyKey
            };

            var created = await documentsRepository.CreateAsync(createDocumentDto);

            if (created is null)
            {
                GetByIdempotencyKeyDto getByIdempotencyKeyDto = new()
                {
                    UserId = userId,
                    IdempotencyKey = idempotencyKey
                };

                Domain.Entities.Document existing = await documentsRepository.GetByIdempotencyKeyAsync(getByIdempotencyKeyDto)
                    ?? throw new DomainValidationException("Documento duplicado não encontrado.");
                return new DocumentUploadResult(existing.Id, Created: false);
            }

            try
            {
                string documentUrl = await fileStorage.SaveAsync(userId, created.Id, imageBytes, cancellationToken) ?? throw new DomainValidationException("Falha ao salvar o arquivo de imagem.");

                UpdateDocumentUrlDto updateDocumentUrlDto = new()
                {
                    Id = created.Id,
                    DocumentUrl = documentUrl,
                };
                Domain.Entities.Document documentsEntity = await documentsRepository.UpdateDocumentUrlAsync(updateDocumentUrlDto) ?? throw new DomainValidationException("Falha ao atualizar a URL do documento.");
            }
            catch
            {
                bool deleted = await documentsRepository.DeleteAsync(created.Id);
                if (!deleted)
                {
                    logger.LogError("Falha ao excluir o documento {DocumentId} após falha ao salvar a imagem.", created.Id);
                }
                throw;
            }

            backgroundTaskQueue.Enqueue(async (scopedProvider, ct) =>
            {
                var uploadService = scopedProvider.GetRequiredService<IDocumentUploadService>() ?? throw new DomainValidationException("Falha ao obter o serviço de upload de documentos.");
                await uploadService.ProcessAsync(created.Id, imageBytes, ct);
            });

            return new DocumentUploadResult(created.Id, Created: true);
        }

        public async Task ProcessAsync(long documentId, byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            try
            {
                await documentsRepository.MarkOcrStartedAsync(documentId, DateTime.UtcNow);

                OcrExtractionResult ocrResult = await ocrService.ExtractAsync(imageBytes, cancellationToken) ?? throw new DomainValidationException("Falha ao extrair texto do OCR.");

                var items = documentParsingService.ParseItems(ocrResult.Text) ?? throw new DomainValidationException("Falha ao analisar os itens do documento.");

                var parsed = string.IsNullOrWhiteSpace(ocrResult.Text) ? null : documentParsingService.Parse(ocrResult.Text, []);
                var valueTrustOcr = ocrResult.DigitConfidence(parsed?.PaymentValueSpan);
                var ocrFinishedAt = DateTime.UtcNow;

                var registration = parsed?.Cnpj is { } cnpj ? await cnpjActivityService.GetAsync(cnpj, cancellationToken) : null;

                unitOfWork.Begin();
                try
                {
                    UpdateOcrResultDto updateOcrResultDto = new()
                    {
                        Id = documentId,
                        TextOcr = ocrResult.Text,
                        TrustOcr = ocrResult.TrustOcr,
                        ValueTrustOcr = valueTrustOcr is { } trust ? Math.Round(trust, 4) : null,
                        OcrFinishedAt = ocrFinishedAt,
                        EstablishmentCnae = registration?.Cnae,
                        EstablishmentRegistryName = registration?.RegistryName,
                    };
                    Domain.Entities.Document updatedDocument = await documentsRepository.UpdateOcrResultAsync(updateOcrResultDto) ?? throw new DomainValidationException("Falha ao atualizar os resultados do OCR.");
                    await documentItemsRepository.ReplaceForDocumentAsync(documentId, items);

                    UpdateStatusDto updateStatusDto = new()
                    {
                        Id = documentId,
                        Status = StatusOCRTypes.processado,
                    };
                    Domain.Entities.Document updatedStatus = await documentsRepository.UpdateStatusAsync(updateStatusDto) ?? throw new DomainValidationException("Falha ao atualizar o status do documento." );
                    await unitOfWork.CommitAsync();
                }
                catch
                {
                    await unitOfWork.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar OCR do documento {DocumentId}", documentId);

                UpdateStatusDto updateStatusDto = new()
                {
                    Id = documentId,
                    Status = StatusOCRTypes.erro,
                };
                await documentsRepository.UpdateStatusAsync(updateStatusDto);
            }
        }
    }
}
