using EventMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventMap.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserBusyDate> UserBusyDates => Set<UserBusyDate>();
    public DbSet<EventJob> EventJobs => Set<EventJob>();
    public DbSet<JobAssignment> JobAssignments => Set<JobAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Name).IsRequired().HasMaxLength(100);
            entity.Property(u => u.PinHash).IsRequired().HasMaxLength(255);
            entity.Property(u => u.AvatarUrl).HasMaxLength(500);
            entity.Property(u => u.ServicesJson).IsRequired().HasMaxLength(1000);
        });

        // UserBusyDate
        modelBuilder.Entity<UserBusyDate>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Reason).HasMaxLength(255);

            entity.HasOne(b => b.User)
                  .WithMany(u => u.BusyDates)
                  .HasForeignKey(b => b.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // EventJob
        modelBuilder.Entity<EventJob>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property(j => j.ServiceCode).IsRequired().HasMaxLength(50);
            entity.Property(j => j.LocationName).IsRequired().HasMaxLength(255);
            entity.Property(j => j.SubAddress).HasMaxLength(500);
            entity.Property(j => j.Latitude).HasPrecision(9, 6);
            entity.Property(j => j.Longitude).HasPrecision(9, 6);
            entity.Property(j => j.ClientTotalRevenue).HasPrecision(18, 0);
            entity.Property(j => j.ExternalWorkersNote).HasMaxLength(500);
            entity.Property(j => j.Note).HasMaxLength(1000);

            entity.HasOne(j => j.CreatedByUser)
                  .WithMany(u => u.JobsCreated)
                  .HasForeignKey(j => j.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(j => j.OwnerUser)
                  .WithMany(u => u.JobsOwned)
                  .HasForeignKey(j => j.OwnerUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // JobAssignment
        modelBuilder.Entity<JobAssignment>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.CastAmount).HasPrecision(18, 0);
            entity.Property(a => a.PersonalNote).HasMaxLength(500);

            entity.HasOne(a => a.Job)
                  .WithMany(j => j.Assignments)
                  .HasForeignKey(a => a.JobId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.User)
                  .WithMany(u => u.Assignments)
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
