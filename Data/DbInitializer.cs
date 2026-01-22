using Microsoft.EntityFrameworkCore;
using VacationTracker.Data.Entities;

namespace VacationTracker.Data;

public static class DbInitializer
{
    public static void Initialize(ApplicationDbContext context)
    {
        context.Database.EnsureCreated();

        // Migration logic for ScheduledStartTime
        using (var connection = context.Database.GetDbConnection())
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info('DraftSessions');";
            using var reader = command.ExecuteReader();
            var hasScheduledStartTime = false;
            while (reader.Read())
            {
                if (reader.GetString(1) == "ScheduledStartTime")
                {
                    hasScheduledStartTime = true;
                    break;
                }
            }

            if (!hasScheduledStartTime)
            {
                context.Database.ExecuteSqlRaw(
                    "ALTER TABLE DraftSessions ADD COLUMN ScheduledStartTime TEXT NULL;");
            }
        }

        // Seed data
        if (!context.Users.Any())
        {
            context.Users.AddRange(
                new User
                {
                    Email = "admin@example.com",
                    Name = "Admin User",
                    Role = Role.Admin,
                    BadgeNumber = 2,
                    WeekQuota = 5,
                    DayQuota = 5
                },
                new User
                {
                    Email = "employee@example.com",
                    Name = "Employee User",
                    Role = Role.Employee,
                    BadgeNumber = 999,
                    WeekQuota = 5,
                    DayQuota = 5
                },
                new User
                {
                    Email = "dispatcher1@example.com",
                    Name = "Dispatcher One",
                    Role = Role.Dispatcher,
                    BadgeNumber = 10,
                    WeekQuota = 5,
                    DayQuota = 5
                },
                new User
                {
                    Email = "dispatcher2@example.com",
                    Name = "Dispatcher Two",
                    Role = Role.Dispatcher,
                    BadgeNumber = 20,
                    WeekQuota = 5,
                    DayQuota = 5
                }
            );
            context.SaveChanges();
        }
    }
}
