using Microsoft.EntityFrameworkCore;
using VacationTracker.Data;
using VacationTracker.Data.Entities;

namespace VacationTracker.Services;

public interface IUserService
{
    Task<User?> GetUserByEmailAsync(string email);
    Task<List<User>> GetUsersAsync();
    Task<List<User>> GetAdminUsersAsync();
    Task<(bool Success, string? ErrorMessage)> CreateUserAsync(User user);
    Task<(bool Success, string? ErrorMessage)> UpdateUserAsync(User user);
    Task<(bool Success, string? ErrorMessage)> DeleteUserAsync(int userId);
}

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<List<User>> GetUsersAsync()
    {
        return await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    public async Task<List<User>> GetAdminUsersAsync()
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.Role == Role.Admin)
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    public async Task<(bool Success, string? ErrorMessage)> CreateUserAsync(User user)
    {
        var normalizedEmail = user.Email.Trim().ToLowerInvariant();
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

        if (emailExists)
        {
            return (false, "A user with that email already exists.");
        }

        user.Email = user.Email.Trim();
        user.Name = user.Name.Trim();

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateUserAsync(User user)
    {
        var normalizedEmail = user.Email.Trim().ToLowerInvariant();
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail && u.Id != user.Id);

        if (emailExists)
        {
            return (false, "A different user already uses that email.");
        }

        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        if (existingUser == null)
        {
            return (false, "User not found.");
        }

        existingUser.Email = user.Email.Trim();
        existingUser.Name = user.Name.Trim();
        existingUser.Role = user.Role;
        existingUser.WeekQuota = user.WeekQuota;
        existingUser.DayQuota = user.DayQuota;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteUserAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return (false, "User not found.");
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return (true, null);
    }
}
