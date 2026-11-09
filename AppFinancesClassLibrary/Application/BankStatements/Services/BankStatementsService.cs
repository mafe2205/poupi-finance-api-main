using AppFinancesClassLibrary.Application.BankAccounts.Services;
using AppFinancesClassLibrary.Application.BankStatements.Entities.Dtos;
using AppFinancesClassLibrary.Application.BankStatements.Entities.Results;
using AppFinancesClassLibrary.Application.BehavioralInsights.Services;
using AppFinancesClassLibrary.Application.Cards.Services;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Enums;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace AppFinancesClassLibrary.Application.BankStatements.Services
{
    public class BankStatementsService(
        IUnitOfWork unitOfWork,
        IBankStatementsRepository bankStatementsRepository,
        IBankAccountsRepository bankAccountsRepository,
        ICardsService cardsService,
        IBankAccountsService bankAccountsService,
        ICurrentUser currentUser,
        IBackgroundTaskQueue backgroundTaskQueue) : IBankStatementsService
    {
        private static readonly CultureInfo PtBr = new("pt-BR");

        private readonly IUnitOfWork unitOfWork = unitOfWork;
        private readonly IBankStatementsRepository bankStatementsRepository = bankStatementsRepository;
        private readonly IBankAccountsRepository bankAccountsRepository = bankAccountsRepository;

        public async Task<BankStatementResult> CreateAsync(CreateBankStatementDto dto)
        {
            BankStatement bankStatement;

            Domain.Validate.NotNull(dto, nameof(CreateBankStatementDto));

            Domain.Validate.Id(dto.CardId, nameof(dto.CardId));
            Domain.Validate.String(dto.MonthReference, nameof(dto.MonthReference));
            Domain.Validate.DateOnly(dto.ClosingDate, nameof(dto.ClosingDate));
            Domain.Validate.DateOnly(dto.DueDate, nameof(dto.DueDate), min: dto.ClosingDate);
            Domain.Validate.Enum(dto.Status, nameof(dto.Status));

            await cardsService.GetByIdAsync(dto.CardId);

            bankStatement = await bankStatementsRepository.CreateAsync(dto.Adapt<CreateBankStatementInternalDto>());
            if (bankStatement is null) throw new DomainValidationException("Extrato bancário não encontrada.");

            return await ToResultAsync(bankStatement);
        }

        public async Task<BankStatementResult> GetByIdAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            BankStatement bankStatement = await GetOwnedAsync(id);
            return await ToResultAsync(bankStatement);
        }

        public async Task<BankStatementResult> UpdateAsync(long id, UpdateBankStatementDto dto)
        {
            Domain.Validate.NotNull(dto, nameof(UpdateBankStatementDto));
            if (id != dto.Id)
                throw new DomainValidationException("O id da rota é diferente do id enviado no corpo.");
            Domain.Validate.Id(dto.Id, nameof(dto.Id));

            var statement = await GetOwnedAsync(dto.Id);
            var remaining = Remaining(statement, await bankStatementsRepository.GetAmountsAsync(dto.Id));
            if (remaining <= 0)
                throw new DomainValidationException("Essa fatura não tem valor a pagar.");

            await PayAsync(dto.Id, new CreateBankStatementPaymentDto
            {
                AmountCents = (int)remaining,
                PaymentDate = dto.PaymentDate
            });

            return await GetByIdAsync(dto.Id);
        }

        public async Task<BankStatementPaymentResult> PayAsync(long id, CreateBankStatementPaymentDto dto)
        {
            BankStatementPayment payment;
            long userId = currentUser.UserId;

            Domain.Validate.NotNull(dto, nameof(CreateBankStatementPaymentDto));
            Domain.Validate.Id(id, nameof(id));
            Domain.Validate.Int(dto.AmountCents, nameof(dto.AmountCents), min: 1);
            Domain.Validate.DateOnly(dto.PaymentDate, nameof(dto.PaymentDate), max: DateOnly.FromDateTime(DateTime.UtcNow));

            var statement = await GetOwnedAsync(id);
            var card = await cardsService.GetByIdAsync(statement.CardId);
            var bankAccountId = dto.BankAccountId ?? card.BankAccountId;
            await bankAccountsService.GetByIdAsync(bankAccountId);

            unitOfWork.Begin();
            try
            {
                var amounts = await bankStatementsRepository.GetAmountsAsync(id);
                statement = await bankStatementsRepository.GetByIdAsync(id);
                var remaining = Remaining(statement, amounts);

                if (remaining <= 0)
                    throw new DomainValidationException("Essa fatura não tem valor a pagar.");
                if (dto.AmountCents > remaining)
                    throw new DomainValidationException($"O valor passa do que falta pagar na fatura ({Money(remaining)}).");

                payment = await bankStatementsRepository.CreatePaymentAsync(new CreateBankStatementPaymentInternalDto
                {
                    BankStatementId = id,
                    BankAccountId = bankAccountId,
                    AmountCents = dto.AmountCents,
                    PaymentDate = dto.PaymentDate
                });

                await bankAccountsRepository.AdjustBalanceAsync(bankAccountId, -dto.AmountCents);

                if (dto.AmountCents == remaining)
                    await bankStatementsRepository.MarkAsPaidAsync(id, dto.PaymentDate);

                await unitOfWork.CommitAsync();
            }
            catch
            {
                await unitOfWork.RollbackAsync();
                throw;
            }

            backgroundTaskQueue.Enqueue((scopedProvider, ct) =>
                scopedProvider.GetRequiredService<IInsightRefreshService>()
                    .RefreshAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow), ct));

            return payment.Adapt<BankStatementPaymentResult>();
        }

        public async Task<IEnumerable<BankStatementPaymentResult>> GetPaymentsAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            await GetOwnedAsync(id);

            var payments = await bankStatementsRepository.GetPaymentsByStatementIdAsync(id) ?? [];
            return payments.Adapt<IEnumerable<BankStatementPaymentResult>>();
        }

        private async Task<BankStatement> GetOwnedAsync(long id)
        {
            BankStatement bankStatement = await bankStatementsRepository.GetByIdAsync(id)
                ?? throw new DomainValidationException("Extrato bancário não encontrada.");
            await cardsService.GetByIdAsync(bankStatement.CardId);

            return bankStatement;
        }

        private async Task<BankStatementResult> ToResultAsync(BankStatement statement)
        {
            var amounts = await bankStatementsRepository.GetAmountsAsync(statement.Id);
            var result = statement.Adapt<BankStatementResult>();

            result.TotalCents = amounts.TotalCents;
            result.PaidCents = amounts.PaidCents;
            result.RemainingCents = Remaining(statement, amounts);
            return result;
        }

        private static long Remaining(BankStatement statement, BankStatementAmounts amounts)
            => statement.Status == BankStatementsStatus.paga ? 0 : Math.Max(0, amounts.TotalCents - amounts.PaidCents);

        private static string Money(long cents) => (cents / 100m).ToString("C", PtBr);
    }
}
