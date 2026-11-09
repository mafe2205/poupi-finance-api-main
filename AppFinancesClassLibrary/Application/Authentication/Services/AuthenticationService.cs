using AppFinancesClassLibrary.Application.Authentication.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Login.Entities.Dtos;
using AppFinancesClassLibrary.Application.Login.Entities.Results;
using AppFinancesClassLibrary.Application.Login.Services;
using AppFinancesClassLibrary.Domain.Exceptions;
using Mapster;

namespace AppFinancesClassLibrary.Application.Authentication.Services
{
    public class AuthenticationService(IAuthenticationRepository autheticationRepository, IPasswordHasher passwordHasher, ITokenService tokenService, ICurrentUser currentUser) : IAuthenticationService
    {
        private readonly IAuthenticationRepository autheticationRepository = autheticationRepository;
        private readonly IPasswordHasher passwordHasher = passwordHasher;
        private readonly ITokenService tokenService = tokenService;

        public async Task<LoginResultDto> LoginAsync(LoginDto dto)
        {
            Domain.Entities.Authentication usuario = await autheticationRepository.GetByUsuarioOuEmailAsync(dto.Identifier);
            if (usuario is null || !passwordHasher.Verify(dto.Password, usuario.PasswordHash))
                throw new UnauthorizedException("Usuário ou senha inválidos.");

            var (token, expiresAt) = tokenService.GenerateToken(usuario.UserId, usuario.Role.ToString());
            return new LoginResultDto
            {
                Token = token,
                UserId = usuario.UserId,
                Role = usuario.Role.ToString(),
                ExpiresAt = expiresAt
            };
        }

        public async Task<string> CheckLoginAsync(string userLoginName, string password)
        {
            try
            {
                Domain.Entities.Authentication usuario = await autheticationRepository.GetByUsuarioOuEmailAsync(userLoginName) ?? throw new UserNotFoundException();
                if (!passwordHasher.Verify(password, usuario.PasswordHash))
                {
                    throw new InvalidOperationException("Credenciais inválidas.");
                }

                var (token, expiresAt) = tokenService.GenerateToken(usuario.UserId, usuario.Role.ToString());
                return token;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Um erro ocorreu ao verificar o login.", ex);
            }
        }

        public async Task ChangePasswordAsync(ChangePasswordDto dto)
        {
            long userId = currentUser.UserId;
            Domain.Validate.NotNull(dto, nameof(ChangePasswordDto));
            Domain.Validate.String(dto.CurrentPassword, nameof(dto.CurrentPassword));
            Domain.Validate.String(dto.NewPassword, nameof(dto.NewPassword), minLength: 6);

            Domain.Entities.Authentication usuario = await autheticationRepository.GetByUserIdAsync(userId)
                ?? throw new UserNotFoundException();

            if (!passwordHasher.Verify(dto.CurrentPassword, usuario.PasswordHash))
                throw new DomainValidationException("Senha atual incorreta.");

            if (dto.NewPassword == dto.CurrentPassword)
                throw new DomainValidationException("A nova senha deve ser diferente da atual.");

            await autheticationRepository.UpdatePasswordAsync(userId, passwordHasher.HashPassword(dto.NewPassword));
        }

        public async Task<AuthenticationResult> UpdateUsernameAsync(long id, string updatedUsername)
        {
            try
            {
                Domain.Entities.Authentication userFound;
                Domain.Entities.Authentication user;

                Domain.Validate.Id(id, nameof(id));
                Domain.Validate.String(updatedUsername, nameof(updatedUsername), minLength: 3, maxLength: 50);

                userFound = await autheticationRepository.GetByIdAsync(id) ?? throw new UserNotFoundException();
                user = await autheticationRepository.UpdateUsernameAsync(id, updatedUsername);
                return user.Adapt<AuthenticationResult>();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Um erro ocorreu ao atualizar o nome de usuário.", ex);
            }
        }

        public async Task<AuthenticationResult> CreateAsync(CreateAutheticationDto dto)
        {
            try
            {
                Domain.Entities.Authentication user;

                Domain.Validate.NotNull(dto, nameof(CreateAutheticationDto));
                Domain.Validate.Id(dto.UserId, nameof(dto.UserId));
                Domain.Validate.String(dto.Username, nameof(dto.Username), minLength: 3, maxLength: 50);
                Domain.Validate.String(dto.PasswordHash, nameof(dto.PasswordHash), minLength: 1);
                Domain.Validate.Enum(dto.Role, nameof(dto.Role));
                if (dto.LastLogin != default)
                    Domain.Validate.DateTime(dto.LastLogin, nameof(dto.LastLogin), max: DateTime.UtcNow);

                user = await autheticationRepository.CreateAsync(dto);
                return user.Adapt<AuthenticationResult>();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Um erro ocorreu ao criar a entidade de autenticação.", ex);
            }
        }
    }
}
