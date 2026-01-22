using Microsoft.EntityFrameworkCore;
using VacationTracker.Data;
using VacationTracker.Data.Entities;

namespace VacationTracker.Services;

public interface IVacationService
{
    Task<List<VacationRequest>> GetRequestsAsync();
    Task<List<VacationRequest>> GetRequestsByUserAsync(int userId);
    Task<(int Taken, int Total)> GetAvailabilityAsync(DateTime date);
    Task<(int Taken, int Total)> GetWeekAvailabilityAsync(DateTime monday);
    Task<(bool Success, string Message)> CreateRequestAsync(VacationRequest request);
    Task<(bool Success, string Message)> UpdateRequestAsync(int requestId, int userId, DateTime startDate, DateTime endDate);
    Task<bool> UpdateStatusAsync(int requestId, Status status);
    Task<bool> DeleteRequestAsync(int requestId);
    Task<bool> DeleteRequestForUserAsync(int requestId, int userId);
}

public class VacationService : IVacationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private const int MaxWeeklyVacations = 5;
    private const int MaxDailyVacations = 1;

    public VacationService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<VacationRequest>> GetRequestsAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.VacationRequests
            .AsNoTracking()
            .Include(r => r.User)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<List<VacationRequest>> GetRequestsByUserAsync(int userId)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.VacationRequests
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<(int Taken, int Total)> GetAvailabilityAsync(DateTime date)
    {
        using var context = _contextFactory.CreateDbContext();
        var targetDate = date.Date;
        var taken = await context.VacationRequests
            .AsNoTracking()
            .Where(r => r.Status == Status.Approved && !r.IsWeekBooking && r.StartDate.Date <= targetDate && r.EndDate.Date >= targetDate)
            .CountAsync();
        return (taken, MaxDailyVacations);
    }

    public async Task<(int Taken, int Total)> GetWeekAvailabilityAsync(DateTime monday)
    {
        using var context = _contextFactory.CreateDbContext();
        var weekStart = GetWeekStart(monday);
        var weekEnd = weekStart.AddDays(6);
        // A week is considered "taken" if a user has a week-booking request that overlaps that week.
        var taken = await context.VacationRequests
            .AsNoTracking()
            .Where(r => r.Status == Status.Approved && r.IsWeekBooking &&
                        ((r.StartDate.Date <= weekEnd && r.EndDate.Date >= weekStart)))
            .Select(r => r.UserId)
            .Distinct()
            .CountAsync();
            
        return (taken, MaxWeeklyVacations);
    }

    public async Task<(bool Success, string Message)> CreateRequestAsync(VacationRequest request)
    {
        using var context = _contextFactory.CreateDbContext();
        // 1. Quota Validation
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.VacationRequests)
            .FirstOrDefaultAsync(u => u.Id == request.UserId);

        if (user == null) return (false, "User not found.");

        var hasOverlap = user.VacationRequests.Any(r =>
            r.Type == RequestType.Vacation &&
            r.Status != Status.Rejected &&
            r.StartDate.Date <= request.EndDate.Date &&
            r.EndDate.Date >= request.StartDate.Date);

        if (hasOverlap)
        {
            return (false, "You already have a vacation request that overlaps these dates.");
        }

        if (request.IsWeekBooking)
        {
            var approvedWeeks = user.VacationRequests
                .Count(r => r.Status == Status.Approved && r.Type == RequestType.Vacation && r.IsWeekBooking);

            if (approvedWeeks + 1 > user.WeekQuota)
            {
                return (false, $"Request exceeds your weekly quota. You have {user.WeekQuota - approvedWeeks} weeks left.");
            }
        }
        else
        {
            var approvedDays = user.VacationRequests
                .Count(r => r.Status == Status.Approved && r.Type == RequestType.Vacation && !r.IsWeekBooking);

            if (approvedDays + 1 > user.DayQuota)
            {
                return (false, $"Request exceeds your single day quota. You have {user.DayQuota - approvedDays} days left.");
            }
        }

        // 2. Capacity Validation
        if (!request.IsWeekBooking)
        {
            var (taken, total) = await GetAvailabilityAsync(request.StartDate);
            if (taken >= total)
            {
                return (false, $"Capacity reached for {request.StartDate.ToShortDateString()}. Max {total} person allowed for single days.");
            }
        }
        else
        {
            var startMonday = GetWeekStart(request.StartDate);

            var (taken, total) = await GetWeekAvailabilityAsync(startMonday);
            if (taken >= total)
            {
                return (false, $"Weekly capacity reached for the week of {startMonday.ToShortDateString()}. Max {total} employees allowed.");
            }
        }

        context.VacationRequests.Add(request);
        await context.SaveChangesAsync();
        return (true, "Request created successfully.");
    }

    public async Task<(bool Success, string Message)> UpdateRequestAsync(int requestId, int userId, DateTime startDate, DateTime endDate)
    {
        using var context = _contextFactory.CreateDbContext();
        if (endDate < startDate)
        {
            return (false, "End date cannot be earlier than start date.");
        }

        var request = await context.VacationRequests.FindAsync(requestId);
        if (request == null || request.UserId != userId)
        {
            return (false, "Request not found.");
        }

        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.VacationRequests)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return (false, "User not found.");

        var hasOverlap = user.VacationRequests.Any(r =>
            r.Id != requestId &&
            r.Type == RequestType.Vacation &&
            r.Status != Status.Rejected &&
            r.StartDate.Date <= endDate.Date &&
            r.EndDate.Date >= startDate.Date);

        if (hasOverlap)
        {
            return (false, "You already have a vacation request that overlaps these dates.");
        }

        if (request.IsWeekBooking)
        {
            var approvedWeeks = user.VacationRequests
                .Count(r => r.Id != requestId && r.Status == Status.Approved && r.Type == RequestType.Vacation && r.IsWeekBooking);

            if (approvedWeeks + 1 > user.WeekQuota)
            {
                return (false, $"Request exceeds your weekly quota. You have {user.WeekQuota - approvedWeeks} weeks left.");
            }
        }
        else
        {
            var approvedDays = user.VacationRequests
                .Count(r => r.Id != requestId && r.Status == Status.Approved && r.Type == RequestType.Vacation && !r.IsWeekBooking);

            if (approvedDays + 1 > user.DayQuota)
            {
                return (false, $"Request exceeds your single day quota. You have {user.DayQuota - approvedDays} days left.");
            }
        }

        if (!request.IsWeekBooking)
        {
            var taken = await context.VacationRequests
                .AsNoTracking()
                .Where(r => r.Id != requestId && r.Status == Status.Approved && !r.IsWeekBooking && r.StartDate.Date <= startDate.Date && r.EndDate.Date >= startDate.Date)
                .CountAsync();

            if (taken >= MaxDailyVacations)
            {
                return (false, $"Capacity reached for {startDate.ToShortDateString()}. Max {MaxDailyVacations} person allowed for single days.");
            }
        }
        else
        {
            var startMonday = GetWeekStart(startDate);
            var sunday = startMonday.AddDays(6);

            var taken = await context.VacationRequests
                .AsNoTracking()
                .Where(r => r.Id != requestId && r.Status == Status.Approved && r.IsWeekBooking &&
                            ((r.StartDate.Date <= sunday && r.EndDate.Date >= startMonday)))
                .Select(r => r.UserId)
                .Distinct()
                .CountAsync();

            if (taken >= MaxWeeklyVacations)
            {
                return (false, $"Weekly capacity reached for the week of {startMonday.ToShortDateString()}. Max {MaxWeeklyVacations} employees allowed.");
            }
        }

        request.StartDate = startDate;
        request.EndDate = endDate;
        request.Status = Status.Pending;
        await context.SaveChangesAsync();
        return (true, "Request updated successfully.");
    }

    public async Task<bool> UpdateStatusAsync(int requestId, Status status)
    {
        using var context = _contextFactory.CreateDbContext();
        var request = await context.VacationRequests.FindAsync(requestId);
        if (request == null) return false;

        request.Status = status;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteRequestAsync(int requestId)
    {
        using var context = _contextFactory.CreateDbContext();
        var request = await context.VacationRequests.FindAsync(requestId);
        if (request == null) return false;

        context.VacationRequests.Remove(request);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteRequestForUserAsync(int requestId, int userId)
    {
        using var context = _contextFactory.CreateDbContext();
        var request = await context.VacationRequests.FindAsync(requestId);
        if (request == null || request.UserId != userId) return false;

        context.VacationRequests.Remove(request);
        await context.SaveChangesAsync();
        return true;
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var day = date.Date;
        var diff = (7 + (int)day.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return day.AddDays(-diff);
    }
}
