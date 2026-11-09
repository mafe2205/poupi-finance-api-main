namespace AppFinancesClassLibrary.Application.User.Entities.Dtos
{
    public record UpdateUserDto
    {
        public string Name { get; set; }
        public string Surname { get; set; }
        public DateOnly BirthDate { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string User { get; set; }
    }
}
