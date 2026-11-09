namespace AppFinancesClassLibrary.Application.User.Entities.Results
{
    public record UserResult
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public DateOnly BirthDate { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }
}
