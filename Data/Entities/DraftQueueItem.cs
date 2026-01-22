using System.ComponentModel.DataAnnotations;

namespace VacationTracker.Data.Entities;

public class DraftQueueItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public DateTime WeekStartDate { get; set; }
    
    public int QueueOrder { get; set; }
}
