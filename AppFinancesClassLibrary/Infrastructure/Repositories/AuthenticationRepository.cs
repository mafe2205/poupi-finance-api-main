using AppFinancesClassLibrary.Application.Authentication.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class AuthenticationRepository(IUnitOfWork unitOfWork) : IAuthenticationRepository
    {
        private IUnitOfWork unitOfWork = unitOfWork;

        #region SQL
        private const string sqlGetById = @"
            SELECT ID, USER_ID, USERNAME, PASSWORD_HASH, ROLE, LAST_LOGIN, CREATED_AT, UPDATED_AT
            FROM AUTHENTICATION
            WHERE ID = :Id";

        private const string sqlGetByUserId = @"
            SELECT a.ID, a.USER_ID, a.USERNAME, a.PASSWORD_HASH, a.ROLE, a.LAST_LOGIN, a.CREATED_AT, a.UPDATED_AT
            FROM AUTHENTICATION a
            INNER JOIN USERS p ON p.ID = a.USER_ID
            WHERE a.USER_ID = :UserId AND p.ACTIVE = true";

        private const string sqlUpdatePassword = @"
            UPDATE AUTHENTICATION SET PASSWORD_HASH = :PasswordHash, UPDATED_AT = :UpdatedAt
            WHERE USER_ID = :UserId";

        private const string sqlUpdateUsername = @"
            UPDATE AUTHENTICATION SET USERNAME = :Username, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id
            RETURNING *";

        private const string sqlUpdateUsernameByUserId = @"
            UPDATE AUTHENTICATION SET USERNAME = :Username, UPDATED_AT = :UpdatedAt
            WHERE USER_ID = :UserId";

        private const string sqlInsert = @"
            INSERT INTO AUTHENTICATION (USER_ID, USERNAME, PASSWORD_HASH, ROLE, CREATED_AT, UPDATED_AT)
            VALUES (:UserId, :Username, :PasswordHash, :Role, :CreatedAt, :UpdatedAt)
            RETURNING *";

        private const string sqlGetByUserOrEmail = @"
            SELECT a.ID, a.USER_ID, a.USERNAME, a.PASSWORD_HASH, a.ROLE, a.LAST_LOGIN, a.CREATED_AT, a.UPDATED_AT
            FROM AUTHENTICATION a
            INNER JOIN USERS p ON p.ID = a.USER_ID
            WHERE (a.USERNAME = :Identificador OR p.EMAIL = :Identificador)
              AND p.ACTIVE = true";
        #endregion

        public Task<Authentication> GetByUserIdAsync(long userId)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Authentication>(sqlGetByUserId, new { UserId = userId }, unitOfWork.Transaction);
        }

        public Task UpdatePasswordAsync(long userId, string passwordHash)
        {
            var param = new { UserId = userId, PasswordHash = passwordHash, UpdatedAt = DateTime.UtcNow };

            return unitOfWork.Connection.ExecuteAsync(sqlUpdatePassword, param, unitOfWork.Transaction);
        }

        public Task<Authentication> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Authentication>(sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public async Task<Authentication> UpdateUsernameAsync(long id, string updatedUsername)
        {
            var param = new { Id = id, Username = updatedUsername, UpdatedAt = DateTime.UtcNow };

            return await unitOfWork.Connection.QuerySingleAsync<Authentication>(sqlUpdateUsername, param, unitOfWork.Transaction);
        }

        public Task UpdateUsernameByUserIdAsync(long userId, string username)
        {
            var param = new { UserId = userId, Username = username, UpdatedAt = DateTime.UtcNow };

            return unitOfWork.Connection.ExecuteAsync(sqlUpdateUsernameByUserId, param, unitOfWork.Transaction);
        }

        public Task<Authentication> CreateAsync(CreateAutheticationDto dto)
        {
            var now = DateTime.UtcNow;
            var param = new
            {
                dto.UserId,
                dto.Username,
                dto.PasswordHash,
                dto.Role,
                CreatedAt = now,
                UpdatedAt = now
            };

            return unitOfWork.Connection.QuerySingleAsync<Authentication>(sqlInsert, param, unitOfWork.Transaction);
        }

        public Task<Authentication> GetByUsuarioOuEmailAsync(string userLoginName)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Authentication>(sqlGetByUserOrEmail, new { Identificador = userLoginName }, unitOfWork.Transaction);
        }
    }
}
