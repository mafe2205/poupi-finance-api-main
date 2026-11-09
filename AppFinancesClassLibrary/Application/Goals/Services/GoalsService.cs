using AppFinancesClassLibrary.Application.Goals.Entities.Dtos;
using AppFinancesClassLibrary.Application.Goals.Entities.Results;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Transactions.Entities.Dtos;
using AppFinancesClassLibrary.Application.Transactions.Entities.Results;
using AppFinancesClassLibrary.Application.User.Entities.Results;
using AppFinancesClassLibrary.Application.User.Services;
using AppFinancesClassLibrary.Domain.Entities;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;

namespace AppFinancesClassLibrary.Application.Goals.Services
{
    public class GoalsService(IGoalsRepository goalsRepository, ITransactionsRepository transactionsRepository, IUserService userService, ICurrentUser currentUser) : IGoalsService
    {
        private readonly IGoalsRepository goalsRepository = goalsRepository;
        private readonly ITransactionsRepository transactionsRepository = transactionsRepository;
        private readonly IUserService userService = userService;

        public async Task<GoalResult> CreateAsync(CreateGoalDto dto)
        {
            Goal goal;

            Domain.Validate.NotNull(dto, nameof(CreateGoalDto));

            dto.UserId = currentUser.ResolveUserId(dto.UserId);

            Domain.Validate.Id(dto.UserId, nameof(dto.UserId));
            Domain.Validate.DateOnly(dto.InicialDate, nameof(dto.InicialDate));
            Domain.Validate.DateOnly(dto.FinalDate, nameof(dto.FinalDate), min: dto.InicialDate);
            Domain.Validate.Int(dto.InicialValueCents, nameof(dto.InicialValueCents), min: 0);
            Domain.Validate.Int(dto.FinalValueCents, nameof(dto.FinalValueCents), min: 1);
            Domain.Validate.String(dto.Description, nameof(dto.Description), minLength: 2, maxLength: 250);

            if (dto.InicialValueCents > dto.FinalValueCents)
                throw new DomainValidationException("Valor inicial não pode ser maior que o valor final.");
            if (dto.InicialDate > dto.FinalDate)
                throw new DomainValidationException("Data inicial não pode ser maior que a data final.");
            if (dto.InicialDate == dto.FinalDate && dto.InicialValueCents == dto.FinalValueCents)
                throw new DomainValidationException("Valor inicial e final não podem ser os mesmos quando as datas iniciais e finais são as mesmas.");

            UserResult userFound = await userService.GetByIdAsync(dto.UserId)
                ?? throw new DomainValidationException("User not found.");

            goal = await goalsRepository.CreateAsync(dto);
            if (goal == null)
                throw new DomainValidationException("Erro ao criar a meta.");

            return goal.Adapt<GoalResult>();
        }

        public async Task<GoalResult> GetByIdAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            Goal goal = await GetOwnedAsync(id);
            return goal.Adapt<GoalResult>();
        }

        public async Task<IEnumerable<GoalResult>> GetAllByUserIdAsync(long userId)
        {
            IEnumerable<Goal> goals;
            Domain.Validate.Id(userId, nameof(userId));
            currentUser.EnsureCanAccess(userId);

            goals = await goalsRepository.GetAllByUserIdAsync(userId) ?? [];
            return goals.Adapt<IEnumerable<GoalResult>>();
        }

        public async Task<IEnumerable<TransactionResult>> GetHistoryAsync(long goalId, TransactionFilterDto filter)
        {
            Goal goal;
            IEnumerable<Transaction> transactions;

            Domain.Validate.Id(goalId, nameof(goalId));
            Domain.Validate.NotNull(filter, nameof(TransactionFilterDto));

            goal = await GetOwnedAsync(goalId);

            filter.MetaId = goalId;

            transactions = await transactionsRepository.GetByUserAsync(goal.UserId, filter) ?? [];
            return transactions.Adapt<IEnumerable<TransactionResult>>();
        }

        public async Task<GoalResult> UpdateAsync(UpdateGoalDto dto)
        {
            Goal goalFound;
            Goal goal;

            Domain.Validate.NotNull(dto, nameof(UpdateGoalDto));
            Domain.Validate.Id(dto.Id, nameof(dto.Id));

            goalFound = await GetOwnedAsync(dto.Id);

            if (dto.FinalDate != default)
                Domain.Validate.DateOnly(dto.FinalDate, nameof(dto.FinalDate), min: goalFound.InicialDate);
            if (dto.FinalValueCents != 0)
                Domain.Validate.Int(dto.FinalValueCents, nameof(dto.FinalValueCents), min: 1);
            if (!string.IsNullOrWhiteSpace(dto.Description))
                Domain.Validate.String(dto.Description, nameof(dto.Description), minLength: 2, maxLength: 250);

            goal = await goalsRepository.UpdateAsync(dto) ?? throw new DomainValidationException("Erro ao atualizar a meta.");
            return goal.Adapt<GoalResult>();
        }

        public async Task<GoalResult> DeleteAsync(long id)
        {
            Domain.Validate.Id(id, nameof(id));

            await GetOwnedAsync(id);

            Goal deactivated = await goalsRepository.DeleteAsync(id) ?? throw new DomainValidationException("Erro ao desativar a meta.");
            return deactivated.Adapt<GoalResult>();
        }

        private async Task<Goal> GetOwnedAsync(long id)
        {
            Goal goal = await goalsRepository.GetByIdAsync(id)
                ?? throw new DomainValidationException("Meta não encontrada.");
            currentUser.EnsureCanAccess(goal.UserId);

            return goal;
        }
    }
}
