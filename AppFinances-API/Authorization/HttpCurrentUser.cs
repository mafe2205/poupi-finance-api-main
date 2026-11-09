using AppFinancesClassLibrary.Application.Interfaces;
using AppFinancesClassLibrary.Domain.Enums;
using AppFinancesClassLibrary.Domain.Exceptions;
using System.Security.Claims;

namespace AppFinances_API.Authorization
{
    public class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
    {
        private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

        public bool IsSystem => httpContextAccessor.HttpContext is null;

        public bool IsAdmin => Principal?.IsInRole(nameof(UserRole.admin)) ?? false;

        public long UserId =>
            long.TryParse(Principal?.FindFirstValue("perfil_id"), out var userId)
                ? userId
                : throw new ForbiddenException("Requisição sem usuário autenticado.");
    }
}
