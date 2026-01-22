using Microsoft.EntityFrameworkCore;
using VacationTracker.Data;
using VacationTracker.Data.Entities;

namespace VacationTracker.Services;

public interface IDraftService
{
    event Action? OnDraftUpdated;
    Task<DraftSession?> GetActiveSessionAsync();
    Task<DraftSession?> GetLatestSessionAsync();
    Task<List<User>> GetDispatcherOrderAsync();
    Task<(bool Success, string Message)> StartDraftAsync(DateTime? scheduledStartTimeUtc);
    Task<bool> PauseDraftAsync();
    Task<bool> ResumeDraftAsync();
    Task<bool> ResetDraftAsync();
    Task<(bool Success, string Message)> MakePickAsync(int userId, DateTime weekStart);
    Task<List<DraftQueueItem>> GetUserQueueAsync(int userId);
    Task<bool> AddToQueueAsync(int userId, DateTime weekStart);
    Task<bool> RemoveFromQueueAsync(int userId, int queueItemId);
    Task<bool> MoveQueueItemAsync(int userId, int queueItemId, bool up);
    Task ProcessTurnTimeoutAsync();
    Task ProcessScheduledDraftsAsync();
}

public class DraftService : IDraftService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    public event Action? OnDraftUpdated;

    public DraftService(IServiceScopeFactory scopeFactory, IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _scopeFactory = scopeFactory;
        _contextFactory = contextFactory;
    }

    public async Task<DraftSession?> GetActiveSessionAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.DraftSessions.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive);
    }

    public async Task<DraftSession?> GetLatestSessionAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.DraftSessions
            .AsNoTracking()
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<User>> GetDispatcherOrderAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.Users
            .AsNoTracking()
            .Where(u => u.Role == Role.Dispatcher)
            .OrderBy(u => u.BadgeNumber)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> StartDraftAsync(DateTime? scheduledStartTimeUtc)
    {
        using var context = _contextFactory.CreateDbContext();

        var existing = await context.DraftSessions
            .AnyAsync(s => s.IsActive || (s.ScheduledStartTime.HasValue && !s.EndTime.HasValue));
        if (existing) return (false, "A draft is already active or scheduled.");

        var dispatchers = await GetDispatcherOrderAsync();
        if (!dispatchers.Any()) return (false, "No dispatchers found to start a draft.");

        if (scheduledStartTimeUtc.HasValue && scheduledStartTimeUtc.Value > DateTime.UtcNow)
        {
            var session = new DraftSession
            {
                IsActive = false,
                ScheduledStartTime = scheduledStartTimeUtc.Value,
                CurrentRound = 1,
                TotalRounds = 5
            };

            context.DraftSessions.Add(session);
            await context.SaveChangesAsync();
            OnDraftUpdated?.Invoke();
            return (true, "Draft scheduled successfully.");
        }

        return await StartDraftSessionAsync(context, dispatchers, DateTime.UtcNow, null);
    }

    private async Task<(bool Success, string Message)> StartDraftSessionAsync(
        ApplicationDbContext context,
        List<User> dispatchers,
        DateTime startTimeUtc,
        DraftSession? existingSession)
    {
        if (!dispatchers.Any()) return (false, "No dispatchers found to start a draft.");

        var session = existingSession ?? new DraftSession();
        session.IsActive = true;
        session.StartTime = startTimeUtc;
        session.CurrentUserId = dispatchers.First().Id;
        session.TurnStartTime = startTimeUtc;
        session.CurrentRound = 1;
        session.TotalRounds = 5;

        if (existingSession == null)
        {
            context.DraftSessions.Add(session);
        }

        await context.SaveChangesAsync();
        OnDraftUpdated?.Invoke();
        return (true, "Draft started successfully.");
    }

    public async Task<bool> PauseDraftAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        var session = await context.DraftSessions.FirstOrDefaultAsync(s => s.IsActive);
        if (session == null) return false;

        session.IsPaused = true;
        await context.SaveChangesAsync();
        OnDraftUpdated?.Invoke();
        return true;
    }

    public async Task<bool> ResumeDraftAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        var session = await context.DraftSessions.FirstOrDefaultAsync(s => s.IsActive);
        if (session == null) return false;

        session.IsPaused = false;
        session.TurnStartTime = DateTime.UtcNow;
        await context.SaveChangesAsync();
        OnDraftUpdated?.Invoke();
        return true;
    }

    public async Task<bool> ResetDraftAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        var sessions = await context.DraftSessions.ToListAsync();
        context.DraftSessions.RemoveRange(sessions);
        await context.SaveChangesAsync();
        OnDraftUpdated?.Invoke();
        return true;
    }

    public async Task<(bool Success, string Message)> MakePickAsync(int userId, DateTime weekStart)
    {
        using var scope = _scopeFactory.CreateScope();
        using var context = _contextFactory.CreateDbContext();
        var vacationService = scope.ServiceProvider.GetRequiredService<IVacationService>();

        var session = await context.DraftSessions.FirstOrDefaultAsync(s => s.IsActive);
        if (session == null || session.IsPaused) return (false, "Draft is not active or is paused.");
        if (session.CurrentUserId != userId) return (false, "It is not your turn.");

        var weekEnd = weekStart.AddDays(6);
        var request = new VacationRequest
        {
            UserId = userId,
            StartDate = weekStart,
            EndDate = weekEnd,
            IsWeekBooking = true,
            Status = Status.Approved,
            Type = RequestType.Vacation,
            Comment = $"Draft Round {session.CurrentRound}"
        };

        var result = await vacationService.CreateRequestAsync(request);
        if (!result.Success) return result;

        await AdvanceTurnAsync(context, session);
        OnDraftUpdated?.Invoke();
        return (true, "Pick successful.");
    }

    private async Task AdvanceTurnAsync(ApplicationDbContext context, DraftSession session)
    {
        var dispatchers = await context.Users
            .Where(u => u.Role == Role.Dispatcher)
            .OrderBy(u => u.BadgeNumber)
            .Select(u => u.Id)
            .ToListAsync();

        var currentIndex = dispatchers.IndexOf(session.CurrentUserId ?? 0);
        
        if (currentIndex == dispatchers.Count - 1)
        {
            if (session.CurrentRound >= session.TotalRounds)
            {
                session.IsActive = false;
                session.EndTime = DateTime.UtcNow;
            }
            else
            {
                session.CurrentRound++;
                session.CurrentUserId = dispatchers.First();
                session.TurnStartTime = DateTime.UtcNow;
            }
        }
        else
        {
            session.CurrentUserId = dispatchers[currentIndex + 1];
            session.TurnStartTime = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    public async Task ProcessTurnTimeoutAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        var session = await context.DraftSessions.FirstOrDefaultAsync(s => s.IsActive && !s.IsPaused);
        
        if (session == null || !session.TurnStartTime.HasValue) return;

        if (DateTime.UtcNow - session.TurnStartTime.Value > TimeSpan.FromMinutes(5))
        {
            var userId = session.CurrentUserId!.Value;
            var queue = await context.DraftQueueItems
                .Where(q => q.UserId == userId)
                .OrderBy(q => q.QueueOrder)
                .ToListAsync();

            bool pickMade = false;
            foreach (var item in queue)
            {
                var result = await MakePickAsync(userId, item.WeekStartDate);
                if (result.Success)
                {
                    context.DraftQueueItems.Remove(item);
                    await context.SaveChangesAsync();
                    pickMade = true;
                    break;
                }
            }

            if (!pickMade)
            {
                await AdvanceTurnAsync(context, session);
                OnDraftUpdated?.Invoke();
            }
        }
    }

    public async Task ProcessScheduledDraftsAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        var session = await context.DraftSessions
            .Where(s => !s.IsActive && s.ScheduledStartTime.HasValue && !s.EndTime.HasValue)
            .OrderBy(s => s.ScheduledStartTime)
            .FirstOrDefaultAsync(s => s.ScheduledStartTime <= DateTime.UtcNow);

        if (session == null) return;

        var dispatchers = await GetDispatcherOrderAsync();
        if (!dispatchers.Any()) return;

        await StartDraftSessionAsync(context, dispatchers, session.ScheduledStartTime!.Value, session);
    }

    public async Task<List<DraftQueueItem>> GetUserQueueAsync(int userId)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.DraftQueueItems
            .AsNoTracking()
            .Where(q => q.UserId == userId)
            .OrderBy(q => q.QueueOrder)
            .ToListAsync();
    }

    public async Task<bool> AddToQueueAsync(int userId, DateTime weekStart)
    {
        using var context = _contextFactory.CreateDbContext();
        
        var exists = await context.DraftQueueItems.AnyAsync(q => q.UserId == userId && q.WeekStartDate == weekStart);
        if (exists) return false;

        var maxOrder = await context.DraftQueueItems
            .Where(q => q.UserId == userId)
            .Select(q => (int?)q.QueueOrder)
            .MaxAsync() ?? 0;

        var item = new DraftQueueItem
        {
            UserId = userId,
            WeekStartDate = weekStart,
            QueueOrder = maxOrder + 1
        };

        context.DraftQueueItems.Add(item);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFromQueueAsync(int userId, int queueItemId)
    {
        using var context = _contextFactory.CreateDbContext();
        var item = await context.DraftQueueItems.FirstOrDefaultAsync(q => q.Id == queueItemId && q.UserId == userId);
        if (item == null) return false;

        context.DraftQueueItems.Remove(item);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MoveQueueItemAsync(int userId, int queueItemId, bool up)
    {
        using var context = _contextFactory.CreateDbContext();
        var items = await context.DraftQueueItems
            .Where(q => q.UserId == userId)
            .OrderBy(q => q.QueueOrder)
            .ToListAsync();

        var item = items.FirstOrDefault(i => i.Id == queueItemId);
        if (item == null) return false;

        var index = items.IndexOf(item);
        if (up && index > 0)
        {
            var prev = items[index - 1];
            (prev.QueueOrder, item.QueueOrder) = (item.QueueOrder, prev.QueueOrder);
        }
        else if (!up && index < items.Count - 1)
        {
            var next = items[index + 1];
            (next.QueueOrder, item.QueueOrder) = (item.QueueOrder, next.QueueOrder);
        }
        else
        {
            return false;
        }

        await context.SaveChangesAsync();
        return true;
    }
}
