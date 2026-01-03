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
    private readonly ApplicationDbContext _context;
    private const int MaxWeeklyVacations = 5;
    private const int MaxDailyVacations = 1;

    public VacationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<VacationRequest>> GetRequestsAsync()
    {
        return await _context.VacationRequests
            .AsNoTracking()
            .Include(r => r.User)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<List<VacationRequest>> GetRequestsByUserAsync(int userId)
    {
        return await _context.VacationRequests
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<(int Taken, int Total)> GetAvailabilityAsync(DateTime date)
    {
        var taken = await _context.VacationRequests
            .AsNoTracking()
            .Where(r => r.Status == Status.Approved && !r.IsWeekBooking && r.StartDate.Date <= date.Date && r.EndDate.Date >= date.Date)
            .CountAsync();
        return (taken, MaxDailyVacations);
    }

    public async Task<(int Taken, int Total)> GetWeekAvailabilityAsync(DateTime monday)
    {
        var sunday = monday.AddDays(6);
        // A week is considered "taken" if a user has a week-booking request that overlaps that week.
        var taken = await _context.VacationRequests
            .AsNoTracking()
            .Where(r => r.Status == Status.Approved && r.IsWeekBooking &&
                        ((r.StartDate.Date <= sunday.Date && r.EndDate.Date >= monday.Date)))
            .Select(r => r.UserId)
            .Distinct()
            .CountAsync();
            
        return (taken, MaxWeeklyVacations);
    }

    public async Task<(bool Success, string Message)> CreateRequestAsync(VacationRequest request)
    {
        // 1. Quota Validation
        var user = await _context.Users
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
            var startMonday = request.StartDate.AddDays(-(int)request.StartDate.DayOfWeek + (int)DayOfWeek.Monday);
            if (request.StartDate.DayOfWeek == DayOfWeek.Sunday) startMonday = request.StartDate.AddDays(-6);

            var (taken, total) = await GetWeekAvailabilityAsync(startMonday);
            if (taken >= total)
            {
                return (false, $"Weekly capacity reached for the week of {startMonday.ToShortDateString()}. Max {total} employees allowed.");
            }
        }

        _context.VacationRequests.Add(request);
        await _context.SaveChangesAsync();
        return (true, "Request created successfully.");
    }

    public async Task<(bool Success, string Message)> UpdateRequestAsync(int requestId, int userId, DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
        {
            return (false, "End date cannot be earlier than start date.");
        }

        var request = await _context.VacationRequests.FindAsync(requestId);
        if (request == null || request.UserId != userId)
        {
            return (false, "Request not found.");
        }

        var user = await _context.Users
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
            var taken = await _context.VacationRequests
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
            var startMonday = startDate.AddDays(-(int)startDate.DayOfWeek + (int)DayOfWeek.Monday);
            if (startDate.DayOfWeek == DayOfWeek.Sunday) startMonday = startDate.AddDays(-6);
            var sunday = startMonday.AddDays(6);

            var taken = await _context.VacationRequests
                .AsNoTracking()
                .Where(r => r.Id != requestId && r.Status == Status.Approved && r.IsWeekBooking &&
                            ((r.StartDate.Date <= sunday.Date && r.EndDate.Date >= startMonday.Date)))
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
        await _context.SaveChangesAsync();
        return (true, "Request updated successfully.");
    }

    public async Task<bool> UpdateStatusAsync(int requestId, Status status)
    {
        var request = await _context.VacationRequests.FindAsync(requestId);
        if (request == null) return false;

        request.Status = status;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteRequestAsync(int requestId)
    {
        var request = await _context.VacationRequests.FindAsync(requestId);
        if (request == null) return false;

        _context.VacationRequests.Remove(request);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteRequestForUserAsync(int requestId, int userId)
    {
        var request = await _context.VacationRequests.FindAsync(requestId);
        if (request == null || request.UserId != userId) return false;

        _context.VacationRequests.Remove(request);
        await _context.SaveChangesAsync();
        return true;
    }
}
