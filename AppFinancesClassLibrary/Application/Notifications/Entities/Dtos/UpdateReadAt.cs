using System.ComponentModel.DataAnnotations;

namespace AppFinancesClassLibrary.Application.Notifications.Entities.Dtos
{
    public class UpdateReadAt
    {
        [Required] public long Id { get; set; }
        [Required] public DateTime ReadAt { get; set; }
    }
}
