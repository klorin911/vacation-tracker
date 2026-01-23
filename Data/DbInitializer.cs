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
                    Email = "supervisor@example.com",
                    Name = "Supervisor User",
                    Role = Role.Supervisor,
                    BadgeNumber = 2,
                    WeekQuota = 5,
                    DayQuota = 5
                },
                new User
                {
                    Email = "dispatcher@example.com",
                    Name = "Dispatcher User",
                    Role = Role.Dispatcher,
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

            for (int i = 3; i <= 10; i++)
            {
                context.Users.Add(new User
                {
                    Email = $"dispatcher{i}@example.com",
                    Name = $"Dispatcher {i}",
                    Role = Role.Dispatcher,
                    BadgeNumber = i * 10,
                    WeekQuota = 5,
                    DayQuota = 5
                });
            }

            context.SaveChanges();
        }
    }
}
