namespace AppFinancesClassLibrary.Application.Transactions.Entities.Results
{
    public class ReclassificationResult
    {
        public int Evaluated { get; set; }

        public List<ReclassifiedTransaction> Changed { get; set; } = [];
    }

    public record ReclassifiedTransaction(long TransactionId, long OldCategoryId, long NewCategoryId);
}
