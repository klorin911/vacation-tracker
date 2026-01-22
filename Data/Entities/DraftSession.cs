using System.ComponentModel.DataAnnotations;

namespace VacationTracker.Data.Entities;

public class DraftSession
{
    public int Id { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsActive { get; set; }
    public bool IsPaused { get; set; }
    
    public int? CurrentUserId { get; set; }
    public DateTime? TurnStartTime { get; set; }
    
    public int CurrentRound { get; set; } = 1;
    public int TotalRounds { get; set; } = 5;
}
