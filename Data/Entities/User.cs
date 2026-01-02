using System.ComponentModel.DataAnnotations;

namespace VacationTracker.Data.Entities;

public class User
{
    public int Id { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public Role Role { get; set; } = Role.User;

    public int TotalQuota { get; set; } = 25;

    public ICollection<VacationRequest> VacationRequests { get; set; } = new List<VacationRequest>();
}
