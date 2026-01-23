using Microsoft.EntityFrameworkCore;
using VacationTracker.Data;
using VacationTracker.Data.Entities;

namespace VacationTracker.Services;

public interface IUserService
{
    Task<User?> GetUserByEmailAsync(string email);
    Task<List<User>> GetUsersAsync();
    Task<List<User>> GetSupervisorUsersAsync();
    Task<(bool Success, string? ErrorMessage)> CreateUserAsync(User user);
    Task<(bool Success, string? ErrorMessage)> UpdateUserAsync(User user);
    Task<(bool Success, string? ErrorMessage)> DeleteUserAsync(int userId);
}

public class UserService : IUserService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public UserService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<List<User>> GetUsersAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.Users
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    public async Task<List<User>> GetSupervisorUsersAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.Users
            .AsNoTracking()
            .Where(u => u.Role == Role.Supervisor)
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    public async Task<(bool Success, string? ErrorMessage)> CreateUserAsync(User user)
    {
        using var context = _contextFactory.CreateDbContext();
        var normalizedEmail = user.Email.Trim().ToLowerInvariant();
        var emailExists = await context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

        if (emailExists)
        {
            return (false, "A user with that email already exists.");
        }

        user.Email = user.Email.Trim();
        user.Name = user.Name.Trim();

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateUserAsync(User user)
    {
        using var context = _contextFactory.CreateDbContext();
        var normalizedEmail = user.Email.Trim().ToLowerInvariant();
        var emailExists = await context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail && u.Id != user.Id);

        if (emailExists)
        {
            return (false, "A different user already uses that email.");
        }

        var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        if (existingUser == null)
        {
            return (false, "User not found.");
        }

        existingUser.Email = user.Email.Trim();
        existingUser.Name = user.Name.Trim();
        existingUser.Role = user.Role;
        existingUser.BadgeNumber = user.BadgeNumber;
        existingUser.WeekQuota = user.WeekQuota;
        existingUser.DayQuota = user.DayQuota;

        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteUserAsync(int userId)
    {
        using var context = _contextFactory.CreateDbContext();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return (false, "User not found.");
        }

        context.Users.Remove(user);
        await context.SaveChangesAsync();
        return (true, null);
    }
}
