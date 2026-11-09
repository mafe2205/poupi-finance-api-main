using AppFinancesClassLibrary.Domain.Enums;

namespace AppFinancesClassLibrary.Application.Transactions
{
    public static class BalanceDelta
    {
        public static int Compute(CategoriesTypes categoryType, GoalContributionDirection? metaDirection, int amountCents)
        {
            if (metaDirection is not null)
                return metaDirection == GoalContributionDirection.retirada ? amountCents : -amountCents;

            return categoryType == CategoriesTypes.Entrada ? amountCents : -amountCents;
        }
    }
}
