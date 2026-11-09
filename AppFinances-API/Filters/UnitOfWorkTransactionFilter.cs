using AppFinancesClassLibrary.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AppFinances_API.Filters
{
    public class UnitOfWorkTransactionFilter(IUnitOfWork unitOfWork) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            unitOfWork.Begin();

            try
            {
                var executedContext = await next();

                if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
                {
                    await unitOfWork.RollbackAsync();
                }
                else
                {
                    await unitOfWork.CommitAsync();
                }
            }
            catch
            {
                await unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}
