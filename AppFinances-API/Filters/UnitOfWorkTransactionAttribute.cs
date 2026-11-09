using Microsoft.AspNetCore.Mvc;

namespace AppFinances_API.Filters
{
    public class UnitOfWorkTransactionAttribute : TypeFilterAttribute
    {
        public UnitOfWorkTransactionAttribute() : base(typeof(UnitOfWorkTransactionFilter))
        {
        }
    }
}
