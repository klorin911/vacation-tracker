using Microsoft.EntityFrameworkCore;
using VacationTracker.Data;
using VacationTracker.Data.Entities;

namespace VacationTracker.Services;

public interface IVacationService
{
    Task<List<VacationRequest>> GetRequestsAsync();
    Task<List<VacationRequest>> GetRequestsByUserAsync(int userId);
    Task<(bool Success, string Message)> CreateRequestAsync(VacationRequest request);
    Task<bool> UpdateStatusAsync(int requestId, Status status);
    Task<bool> DeleteRequestAsync(int requestId);
}

public class VacationService : IVacationService
{
    private readonly ApplicationDbContext _context;
    private const int MaxConcurrentVacations = 3; // Example capacity limit

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

    public async Task<(bool Success, string Message)> CreateRequestAsync(VacationRequest request)
    {
        // 1. Quota Validation
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.VacationRequests)
            .FirstOrDefaultAsync(u => u.Id == request.UserId);

        if (user == null) return (false, "User not found.");

        var requestedDays = (request.EndDate - request.StartDate).Days + 1;
        var approvedDays = user.VacationRequests
            .Where(r => r.Status == Status.Approved && r.Type == RequestType.Vacation)
            .Sum(r => (r.EndDate - r.StartDate).Days + 1);

        if (approvedDays + requestedDays > user.TotalQuota)
        {
            return (false, $"Request exceeds your remaining quota. You have {user.TotalQuota - approvedDays} days left.");
        }

        // 2. Capacity Validation (Simple check: max 3 people per day)
        for (var date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
        {
            var concurrentCount = await _context.VacationRequests
                .AsNoTracking()
                .Where(r => r.Status == Status.Approved && r.StartDate <= date && r.EndDate >= date)
                .CountAsync();

            if (concurrentCount >= MaxConcurrentVacations)
            {
                return (false, $"Capacity reached on {date.ToShortDateString()}. Max {MaxConcurrentVacations} people allowed.");
            }
        }

        _context.VacationRequests.Add(request);
        await _context.SaveChangesAsync();
        return (true, "Request created successfully.");
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
}
