using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using AppFinancesClassLibrary.Application.Notifications.Entities.Dtos;
using AppFinancesClassLibrary.Domain.Entities;
using Dapper;

namespace AppFinancesClassLibrary.Infrastructure.Repositories
{
    public class NotificationsRepository(IUnitOfWork unitOfWork) : INotificationsRepository
    {
        private IUnitOfWork unitOfWork = unitOfWork;

        #region "SQL"
        private const string sqlGetById = @"
            SELECT ID, USER_ID, TYPE, TITLE, MESSAGE, RELATED_ENTITY_ID, READ_AT, CREATED_AT, UPDATED_AT
            FROM NOTIFICATIONS
            WHERE ID = :Id";

        private const string sqlGetAllByUserId = @"
            SELECT ID, USER_ID, TYPE, TITLE, MESSAGE, RELATED_ENTITY_ID, READ_AT, CREATED_AT, UPDATED_AT
            FROM NOTIFICATIONS
            WHERE USER_ID = :UserId
            ORDER BY CREATED_AT DESC";

        private const string sqlInsert = @"
            INSERT INTO NOTIFICATIONS (USER_ID, TYPE, TITLE, MESSAGE, RELATED_ENTITY_ID, READ_AT, CREATED_AT)
            VALUES (:UserId, :Type, :Title, :Message, :RelatedEntityId, :ReadAt, :CreatedAt)
            RETURNING ID, USER_ID, TYPE, TITLE, MESSAGE, RELATED_ENTITY_ID, READ_AT, CREATED_AT, UPDATED_AT";

        private const string sqlUpdateReadAt = @"
            UPDATE NOTIFICATIONS SET
                READ_AT = :ReadAt, UPDATED_AT = :UpdatedAt
            WHERE ID = :Id
            RETURNING ID, USER_ID, TYPE, TITLE, MESSAGE, RELATED_ENTITY_ID, READ_AT, CREATED_AT, UPDATED_AT";

        private const string sqlGetUnreadCount = "SELECT COUNT(*) FROM NOTIFICATIONS WHERE USER_ID = :UserId AND READ_AT IS NULL";

        private const string sqlDelete = "DELETE FROM NOTIFICATIONS WHERE ID = :Id";
        #endregion

        public Task<Notification> GetByIdAsync(long id)
        {
            return unitOfWork.Connection.QueryFirstOrDefaultAsync<Notification>(sqlGetById, new { Id = id }, unitOfWork.Transaction);
        }

        public Task<IEnumerable<Notification>> GetAllByUserIdAsync(long userId)
        {
            return unitOfWork.Connection.QueryAsync<Notification>(sqlGetAllByUserId, new { UserId = userId }, unitOfWork.Transaction);
        }

        public Task<Notification> CreateAsync(CreateNotificationDto dto)
        {
            var param = new
            {
                dto.UserId,
                dto.Type,
                dto.Title,
                dto.Message,
                dto.RelatedEntityId,
                dto.ReadAt,
                CreatedAt = DateTime.UtcNow
            };

            return unitOfWork.Connection.QuerySingleAsync<Notification>(sqlInsert, param, unitOfWork.Transaction);
        }

        public Task<Notification> UpdateReadAtAsync(UpdateReadAt dto)
        {
            var param = new { dto.Id, dto.ReadAt, UpdatedAt = DateTime.UtcNow };

            return unitOfWork.Connection.QuerySingleAsync<Notification>(sqlUpdateReadAt, param, unitOfWork.Transaction);
        }

        public Task<int> GetUnreadCountAsync(long userId)
        {
            return unitOfWork.Connection.ExecuteScalarAsync<int>(sqlGetUnreadCount, new { UserId = userId }, unitOfWork.Transaction);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var rowsAffected = await unitOfWork.Connection.ExecuteAsync(sqlDelete, new { Id = id }, unitOfWork.Transaction);
            return rowsAffected > 0;
        }
    }
}