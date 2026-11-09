using AppFinancesClassLibrary.Application.Authentication.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.User.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;
using Dapper;
using System.Text;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class UserRepository(IUnitOfWork unitOfWork) : IUserRepository
    {
        private IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetById = "SELECT * FROM USERS WHERE ID = :Id AND ACTIVE = true";

        private const string sqlGetByEmail = "SELECT * FROM USERS WHERE EMAIL = :Email AND ACTIVE = true";

        private const string sqlGetAllActiveIds = "SELECT ID FROM USERS WHERE ACTIVE = true";

        // Desativa também o login: o username só precisa ser único entre usuários ativos
        // (índice parcial em AUTHENTICATION.ACTIVE), então pode ser reaproveitado.
        private const string sqlSoftDelete = @"
            WITH deleted_user AS (
                UPDATE USERS SET DELETED_AT = :DeletedAt, ACTIVE = false
                WHERE ID = :Id
            )
            UPDATE AUTHENTICATION SET ACTIVE = false, UPDATED_AT = :DeletedAt
            WHERE USER_ID = :Id";

        private const string sqlInsert = @"
            INSERT INTO USERS (NAME, SURNAME, BIRTHDATE, EMAIL, PHONE, ACTIVE, CREATED_AT, UPDATED_AT)
            VALUES (:Name, :Surname, :BirthDate, :Email, :Phone, true, :CreatedAt, :UpdatedAt)
            RETURNING *";

        private static string BuildUpdateSql(UpdateUserDto dto)
        {
            var sql = new StringBuilder("UPDATE USERS SET ");

            var sets = new List<string>();

            if (dto.Name is not null)
                sets.Add("NAME = :Name");

            if (dto.Surname is not null)
                sets.Add("SURNAME = :Surname");

            if (dto.BirthDate != default)
                sets.Add("BIRTHDATE = :BirthDate");

            if (dto.Email is not null)
                sets.Add("EMAIL = :Email");

            if (dto.Phone is not null)
                sets.Add("PHONE = :Phone");

            sets.Add("UPDATED_AT = :UpdatedAt");

            sql.Append(string.Join(", ", sets));
            // Usuário excluído (soft delete) não pode ser editado.
            sql.Append(" WHERE ID = :Id AND ACTIVE = true RETURNING *");

            return sql.ToString();
        }
        #endregion

        public Task<User> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<User>(sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<User> GetByEmailAsync(string email)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<User>(sqlGetByEmail, new { Email = email }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<long>> GetAllActiveIdsAsync()
        {
            return unitOfWork.Connection.QueryAsync<long>(sqlGetAllActiveIds, transaction: unitOfWork.Transaction);
        }

        public Task<User> CreateAsync(CreateUserDto dto)
        {
            var now = DateTime.UtcNow;

            var param = new
            {
                dto.Name,
                dto.Surname,
                dto.BirthDate,
                dto.Email,
                dto.Phone,
                CreatedAt = now,
                UpdatedAt = now
            };

            return unitOfWork.Connection.QuerySingleAsync<User>(sqlInsert, param, unitOfWork.Transaction);
        }

        public Task<User> UpdateAsync(long id, UpdateUserDto dto)
        {
            var sql = BuildUpdateSql(dto);

            var param = new
            {
                Id = id,
                dto.Name,
                dto.Surname,
                dto.BirthDate,
                dto.Email,
                dto.Phone,
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleOrDefaultAsync<User>(sql, param, unitOfWork.Transaction);
        }

        public Task DeleteAsync(long id)
        {
            var param = new { Id = id, DeletedAt = DateTime.UtcNow };

            return unitOfWork.Connection.ExecuteAsync(sqlSoftDelete, param, unitOfWork.Transaction);
        }

    
    }
}