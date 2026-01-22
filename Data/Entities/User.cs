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

    public Role Role { get; set; } = Role.Employee;

    public int BadgeNumber { get; set; }

    public int WeekQuota { get; set; } = 5;

    public int DayQuota { get; set; } = 5;

    public ICollection<VacationRequest> VacationRequests { get; set; } = new List<VacationRequest>();

    public ICollection<DraftQueueItem> DraftQueueItems { get; set; } = new List<DraftQueueItem>();
}
