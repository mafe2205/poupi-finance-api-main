using AppFinancesClassLibrary.Domain.Exceptions;

namespace AppFinancesClassLibrary.Application.Interfaces
{
    public interface ICurrentUser
    {
        bool IsSystem { get; }
        long UserId { get; }
        bool IsAdmin { get; }
    }

    public static class CurrentUserExtensions
    {
        public static void EnsureCanAccess(this ICurrentUser currentUser, long ownerUserId)
        {
            if (currentUser.IsSystem || currentUser.IsAdmin || currentUser.UserId == ownerUserId)
                return;

            throw new ForbiddenException("Você não tem acesso a este registro.");
        }

        public static long ResolveUserId(this ICurrentUser currentUser, long userIdFromSystem)
            => currentUser.IsSystem ? userIdFromSystem : currentUser.UserId;
    }
}
