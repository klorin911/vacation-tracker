using System.ComponentModel.DataAnnotations;

namespace VacationTracker.Data.Entities;

public class VacationRequest
{
    public int Id { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public RequestType Type { get; set; } = RequestType.Vacation;

    public Status Status { get; set; } = Status.Pending;

    public string? Comment { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
