using AppFinancesClassLibrary.Application.BankAccounts.Entities.Dtos;
using AppFinancesClassLibrary.Application.BankAccounts.Services;
using AppFinancesClassLibrary.Application.Cards.Entities.Dtos;
using AppFinancesClassLibrary.Application.Cards.Entities.Results;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;

namespace AppFinancesClassLibrary.Application.Cards.Services
{
    public class CardsService(ICardsRepository cardsRepository, IBankAccountsService bankAccountsService, ICurrentUser currentUser) : ICardsService
    {
        private readonly ICardsRepository cardsRepository = cardsRepository;
        private readonly IBankAccountsService bankAccountsService = bankAccountsService;

        public async Task<CardResult> CreateAsync(CreateCardDto dto)
        {
            BankAccountResult bankAccount;
            Card card;

            Domain.Validate.NotNull(dto, nameof(CreateCardDto));

            if (dto.BankAccountId <= 0)
                throw new DomainValidationException("BankAccountId é obrigatório: informe o id da conta bancária do cartão.");
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length < 2 || dto.Name.Length > 100)
                throw new DomainValidationException("Name é obrigatório e deve ter entre 2 e 100 caracteres.");
            if (!Enum.IsDefined(dto.Type))
                throw new DomainValidationException("Type inválido: use 0 (crédito) ou 1 (débito).");
            if (dto.LimitValueCents < 0)
                throw new DomainValidationException("LimitValueCents não pode ser negativo.");
            if (dto.ClosingDate is < 1 or > 31)
                throw new DomainValidationException("ClosingDate é obrigatório: informe o dia de fechamento da fatura (1 a 31).");
            if (dto.DueDate is < 1 or > 31)
                throw new DomainValidationException("DueDate é obrigatório: informe o dia de vencimento da fatura (1 a 31).");

            bankAccount = await bankAccountsService.GetByIdAsync(dto.BankAccountId);

            if (bankAccount.IsDefault)
                throw new DomainValidationException("Não é possível criar cartões para a Carteira: escolha uma conta bancária.");

            card = await cardsRepository.CreateAsync(dto);
            if (card is null) throw new NotFoundException("Erro na criação do cartão.");

            return card.Adapt<CardResult>();
        }

        public async Task<CardResult> GetByIdAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            var card = await GetActiveOwnedAsync(id);
            return card.Adapt<CardResult>();
        }

        public async Task<IEnumerable<CardResult>> GetAllByBankAccountIdAsync(long bankAccountId)
        {
            IEnumerable<Card> cards;
            Domain.Validate.Id(bankAccountId, nameof(bankAccountId));

            await bankAccountsService.GetByIdIncludingInactiveAsync(bankAccountId);

            cards = await cardsRepository.GetAllByBankAccountIdAsync(bankAccountId) ?? [];
            return cards.Adapt<IEnumerable<CardResult>>();
        }

        public async Task<CardResult> UpdateAsync(UpdateCardDto dto)
        {
            Card card;

            Domain.Validate.NotNull(dto, nameof(UpdateCardDto));
            Domain.Validate.Id(dto.Id, nameof(dto.Id));

            await GetActiveOwnedAsync(dto.Id);

            if (!string.IsNullOrWhiteSpace(dto.Name))
                Domain.Validate.String(dto.Name, nameof(dto.Name), minLength: 2, maxLength: 100);
            if (dto.Type != default)
                Domain.Validate.Enum(dto.Type, nameof(dto.Type));
            if (dto.LimitValueCents != 0)
                Domain.Validate.Int(dto.LimitValueCents, nameof(dto.LimitValueCents), min: 0);
            if (dto.ClosingDate != 0)
                Domain.Validate.Int(dto.ClosingDate, nameof(dto.ClosingDate), min: 1, max: 31);
            if (dto.DueDate != 0)
                Domain.Validate.Int(dto.DueDate, nameof(dto.DueDate), min: 1, max: 31);

            if (dto.Name is null && dto.Type == default && dto.LimitValueCents == 0 && dto.ClosingDate == 0 && dto.DueDate == 0)
                throw new DomainValidationException("Nenhum campo foi fornecido para atualização.");

            card = await cardsRepository.UpdateAsync(dto);
            if (card is null) throw new NotFoundException("Erro na atualização do cartão.");

            return card.Adapt<CardResult>();
        }

        public async Task<CardResult> DeleteAsync(long id)
        {
            Card deactivated;

            Domain.Validate.Id(id, nameof(id));

            await GetActiveOwnedAsync(id);

            deactivated = await cardsRepository.DeleteAsync(id);
            if (deactivated is null) throw new NotFoundException("Erro na exclusão do cartão.");

            return deactivated.Adapt<CardResult>();
        }

        public async Task<IEnumerable<CardResult>> GetAllInactiveByUserIdAsync(long userId)
        {
            Domain.Validate.Id(userId, nameof(userId));
            currentUser.EnsureCanAccess(userId);

            var cards = await cardsRepository.GetAllInactiveByUserIdAsync(userId);
            return cards.Adapt<IEnumerable<CardResult>>();
        }

        public async Task<CardResult> ReactivateAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            var existing = await cardsRepository.GetByIdIncludingInactiveAsync(id)
                ?? throw new NotFoundException("Cartão não encontrado.");
            var bankAccount = await bankAccountsService.GetByIdIncludingInactiveAsync(existing.BankAccountId);

            if (existing.Active) throw new DomainValidationException("O cartão já está ativo.");
            if (!bankAccount.Active)
                throw new DomainValidationException("Reative a conta bancária antes de reativar o cartão.");

            var reactivated = await cardsRepository.ReactivateAsync(id)
                ?? throw new DomainValidationException("O cartão já está ativo.");

            return reactivated.Adapt<CardResult>();
        }

        private async Task<Card> GetActiveOwnedAsync(long id)
        {
            var card = await cardsRepository.GetByIdAsync(id)
                ?? throw new DomainValidationException("Cartão não encontrado.");
            await bankAccountsService.GetByIdIncludingInactiveAsync(card.BankAccountId);

            return card;
        }
    }
}
