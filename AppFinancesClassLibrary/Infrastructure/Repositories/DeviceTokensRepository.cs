using AppFinancesClassLibrary.Application.DeviceTokens.Entities.Dtos;
using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Domain.Entities;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class DeviceTokensRepository(IUnitOfWork unitOfWork) : IDeviceTokensRepository
    {
        private IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetById = "SELECT * FROM DEVICE_TOKENS WHERE ID = :Id";
        private const string sqlGetByUserIdAndToken = "SELECT * FROM DEVICE_TOKENS WHERE USER_ID = :UserId AND TOKEN = :Token";
        private const string sqlGetByUserId = "SELECT * FROM DEVICE_TOKENS WHERE USER_ID = :UserId";

        private const string sqlInsert = @"
            INSERT INTO DEVICE_TOKENS (USER_ID, TOKEN, PLATFORM, CREATED_AT, UPDATED_AT)
            VALUES (:UserId, :Token, :Platform, :CreatedAt, :UpdatedAt)
            RETURNING ID, USER_ID, TOKEN, PLATFORM, CREATED_AT, UPDATED_AT";

        private const string sqlUpdate = @"
            UPDATE DEVICE_TOKENS SET
                UPDATED_AT = :UpdatedAt
            WHERE USER_ID = :UserId AND TOKEN = :Token
            RETURNING ID, USER_ID, TOKEN, PLATFORM, CREATED_AT, UPDATED_AT";

        private const string sqlDelete = "DELETE FROM DEVICE_TOKENS WHERE ID = :Id";
        #endregion

        public Task<DeviceToken> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<DeviceToken>(sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<DeviceToken> GetByUserIdAndTokenAsync(long userId, string token)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<DeviceToken>(sqlGetByUserIdAndToken, new { UserId = userId, Token = token }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<DeviceToken>> GetByUserIdAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<DeviceToken>(sqlGetByUserId, new { UserId = userId }, unitOfWork.Transaction);
        }

        public Task<DeviceToken> CreateAsync(CreateDeviceTokenDto dto)
        {
            var now = DateTime.UtcNow;

            var param = new
            {
                dto.UserId,
                dto.Token,
                dto.Platform,
                CreatedAt = now,
                UpdatedAt = now
            };

            return unitOfWork.Connection.QuerySingleAsync<DeviceToken>(sqlInsert, param, unitOfWork.Transaction);
        }

        public Task<DeviceToken> UpdateAsync(UpdateDeviceTokenDto dto)
        {
            var param = new
            {
                dto.UserId,
                dto.Token,
                UpdatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleAsync<DeviceToken>(sqlUpdate, param, unitOfWork.Transaction);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var rowsAffected = await unitOfWork.Connection.ExecuteAsync(sqlDelete, new { Id = id }, unitOfWork.Transaction);
            return rowsAffected > 0;
        }
    }
}