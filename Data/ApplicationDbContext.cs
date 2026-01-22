using Microsoft.EntityFrameworkCore;
using VacationTracker.Data.Entities;

namespace VacationTracker.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<VacationRequest> VacationRequests => Set<VacationRequest>();
    public DbSet<DraftSession> DraftSessions => Set<DraftSession>();
    public DbSet<DraftQueueItem> DraftQueueItems => Set<DraftQueueItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<VacationRequest>(entity =>
        {
            entity.HasOne(d => d.User)
                .WithMany(p => p.VacationRequests)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DraftQueueItem>(entity =>
        {
            entity.HasOne(d => d.User)
                .WithMany(p => p.DraftQueueItems)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
