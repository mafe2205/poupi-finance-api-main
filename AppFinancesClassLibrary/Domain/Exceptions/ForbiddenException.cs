namespace AppFinancesClassLibrary.Domain.Exceptions
{
    public class ForbiddenException : Exception
    {
        public ForbiddenException() : base("Access denied.") { }

        public ForbiddenException(string message) : base(message) { }

        public ForbiddenException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
