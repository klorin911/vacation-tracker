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
    Task<(bool Success, string Message)> UndoPickAsync(int userId, DateTime weekStart);
    Task<(bool Success, string Message)> EndTurnAsync(int userId);
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
        return await MakePickInternalAsync(userId, weekStart, allowConsecutivePicks: true);
    }

    public async Task<(bool Success, string Message)> UndoPickAsync(int userId, DateTime weekStart)
    {
        using var context = _contextFactory.CreateDbContext();

        var session = await context.DraftSessions.FirstOrDefaultAsync(s => s.IsActive);
        if (session == null || session.IsPaused) return (false, "Draft is not active or is paused.");
        if (session.CurrentUserId != userId) return (false, "It is not your turn.");
        if (!session.TurnStartTime.HasValue) return (false, "Turn start time is missing.");

        var pick = await GetDraftPickQuery(context, session)
            .Where(r => r.UserId == userId && r.StartDate.Date == weekStart.Date)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (pick == null) return (false, "Pick not found.");
        if (pick.CreatedAt < session.TurnStartTime.Value)
        {
            return (false, "Only picks from the current turn can be undone.");
        }

        context.VacationRequests.Remove(pick);
        await context.SaveChangesAsync();
        OnDraftUpdated?.Invoke();
        return (true, "Pick undone.");
    }

    public async Task<(bool Success, string Message)> EndTurnAsync(int userId)
    {
        using var context = _contextFactory.CreateDbContext();

        var session = await context.DraftSessions.FirstOrDefaultAsync(s => s.IsActive);
        if (session == null || session.IsPaused) return (false, "Draft is not active or is paused.");
        if (session.CurrentUserId != userId) return (false, "It is not your turn.");

        if (!session.TurnStartTime.HasValue)
        {
            return (false, "Turn start time is missing.");
        }

        var picksThisTurn = await GetDraftPickQuery(context, session)
            .Where(r => r.UserId == userId && r.CreatedAt >= session.TurnStartTime.Value)
            .CountAsync();

        if (picksThisTurn == 0)
        {
            return (false, "Make a pick before ending your turn.");
        }

        await AdvanceTurnAsync(context, session);
        OnDraftUpdated?.Invoke();
        return (true, "Turn ended.");
    }

    private async Task<(bool Success, string Message)> MakePickInternalAsync(
        int userId,
        DateTime weekStart,
        bool allowConsecutivePicks)
    {
        using var scope = _scopeFactory.CreateScope();
        using var context = _contextFactory.CreateDbContext();
        var vacationService = scope.ServiceProvider.GetRequiredService<IVacationService>();

        var session = await context.DraftSessions.FirstOrDefaultAsync(s => s.IsActive);
        if (session == null || session.IsPaused) return (false, "Draft is not active or is paused.");
        if (session.CurrentUserId != userId) return (false, "It is not your turn.");

        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return (false, "User not found.");

        var maxPicks = Math.Min(session.TotalRounds, user.WeekQuota);
        var draftPicks = await GetDraftPicksForUserAsync(context, session, userId);
        if (draftPicks.Count >= maxPicks) return (false, "You have already used all your draft picks.");

        if (allowConsecutivePicks && session.TurnStartTime.HasValue)
        {
            var turnPicks = draftPicks
                .Where(r => r.CreatedAt >= session.TurnStartTime.Value)
                .ToList();

            if (turnPicks.Any() && !turnPicks.Any(p => IsConsecutiveWeek(p.StartDate, weekStart)))
            {
                return (false, "Non-consecutive picks must be taken one at a time.");
            }
        }

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

        if (!allowConsecutivePicks)
        {
            await AdvanceTurnAsync(context, session);
            OnDraftUpdated?.Invoke();
            return (true, "Pick successful.");
        }

        var updatedPicks = draftPicks.Select(r => r.StartDate.Date).ToHashSet();
        updatedPicks.Add(weekStart.Date);
        var remainingPicks = maxPicks - updatedPicks.Count;
        var hasAdjacentOption = await HasAdjacentAvailableWeekAsync(
            context,
            vacationService,
            userId,
            updatedPicks);

        if (remainingPicks <= 0 || !hasAdjacentOption)
        {
            await AdvanceTurnAsync(context, session);
        }

        OnDraftUpdated?.Invoke();
        return (true, "Pick successful.");
    }

    private async Task AdvanceTurnAsync(ApplicationDbContext context, DraftSession session)
    {
        var dispatchers = await context.Users
            .Where(u => u.Role == Role.Dispatcher)
            .OrderBy(u => u.BadgeNumber)
            .Select(u => new { u.Id, u.WeekQuota })
            .ToListAsync();

        if (!dispatchers.Any())
        {
            session.IsActive = false;
            session.EndTime = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return;
        }

        var pickCounts = await GetDraftPickCountsAsync(context, session, dispatchers.Select(d => d.Id).ToList());
        var currentIndex = dispatchers.FindIndex(d => d.Id == session.CurrentUserId);
        if (currentIndex < 0) currentIndex = 0;

        bool wrapped = false;
        int nextIndex = currentIndex;
        int attempts = 0;
        while (attempts < dispatchers.Count)
        {
            nextIndex = (nextIndex + 1) % dispatchers.Count;
            if (nextIndex == 0) wrapped = true;

            var dispatcher = dispatchers[nextIndex];
            var maxPicks = Math.Min(session.TotalRounds, dispatcher.WeekQuota);
            pickCounts.TryGetValue(dispatcher.Id, out var count);
            if (count < maxPicks)
            {
                break;
            }

            attempts++;
        }

        if (attempts >= dispatchers.Count)
        {
            session.IsActive = false;
            session.EndTime = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return;
        }

        if (wrapped)
        {
            if (session.CurrentRound >= session.TotalRounds)
            {
                session.IsActive = false;
                session.EndTime = DateTime.UtcNow;
                await context.SaveChangesAsync();
                return;
            }

            session.CurrentRound++;
        }

        session.CurrentUserId = dispatchers[nextIndex].Id;
        session.TurnStartTime = DateTime.UtcNow;
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
                var result = await MakePickInternalAsync(userId, item.WeekStartDate, allowConsecutivePicks: false);
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

    private static IQueryable<VacationRequest> GetDraftPickQuery(ApplicationDbContext context, DraftSession session)
    {
        var startTime = session.StartTime ?? DateTime.MinValue;
        return context.VacationRequests.Where(r =>
            r.IsWeekBooking &&
            r.Type == RequestType.Vacation &&
            r.Status == Status.Approved &&
            r.Comment != null &&
            r.Comment.StartsWith("Draft Round") &&
            r.CreatedAt >= startTime);
    }

    private static async Task<List<VacationRequest>> GetDraftPicksForUserAsync(
        ApplicationDbContext context,
        DraftSession session,
        int userId)
    {
        return await GetDraftPickQuery(context, session)
            .Where(r => r.UserId == userId)
            .ToListAsync();
    }

    private static async Task<Dictionary<int, int>> GetDraftPickCountsAsync(
        ApplicationDbContext context,
        DraftSession session,
        List<int> userIds)
    {
        return await GetDraftPickQuery(context, session)
            .Where(r => userIds.Contains(r.UserId))
            .GroupBy(r => r.UserId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);
    }

    private static bool IsConsecutiveWeek(DateTime first, DateTime second)
    {
        return Math.Abs((first.Date - second.Date).TotalDays) == 7;
    }

    private static async Task<bool> HasAdjacentAvailableWeekAsync(
        ApplicationDbContext context,
        IVacationService vacationService,
        int userId,
        HashSet<DateTime> pickedWeekStarts)
    {
        var candidates = pickedWeekStarts
            .SelectMany(date => new[] { date.AddDays(-7), date.AddDays(7) })
            .Select(date => date.Date)
            .Distinct()
            .Where(date => !pickedWeekStarts.Contains(date))
            .ToList();

        foreach (var candidate in candidates)
        {
            var weekEnd = candidate.AddDays(6);
            var hasOverlap = await context.VacationRequests
                .AnyAsync(r =>
                    r.UserId == userId &&
                    r.Type == RequestType.Vacation &&
                    r.Status != Status.Rejected &&
                    r.StartDate.Date <= weekEnd &&
                    r.EndDate.Date >= candidate);

            if (hasOverlap) continue;

            var (taken, total) = await vacationService.GetWeekAvailabilityAsync(candidate);
            if (taken < total) return true;
        }

        return false;
    }
}
