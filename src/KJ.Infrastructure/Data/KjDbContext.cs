using KJ.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KJ.Infrastructure.Data;

public sealed class KjDbContext : IdentityDbContext
{
    public KjDbContext(DbContextOptions<KjDbContext> options) : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<TagHistory> TagHistory => Set<TagHistory>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<RecipeParameter> RecipeParameters => Set<RecipeParameter>();

    public DbSet<Alarm> Alarms => Set<Alarm>();

    public DbSet<AlarmHistory> AlarmHistory => Set<AlarmHistory>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();

    public DbSet<WorkflowRunStep> WorkflowRunSteps => Set<WorkflowRunStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PropertiesJson).HasColumnType("nvarchar(max)");
            entity.OwnsOne(e => e.Address, a =>
            {
                a.Property(p => p.Host).HasMaxLength(256).IsRequired();
            });
            entity.HasMany(e => e.Tags)
                .WithOne(t => t.Device)
                .HasForeignKey(t => t.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Address).HasMaxLength(256);
            entity.HasIndex(e => new { e.DeviceId, e.Name }).IsUnique();
        });

        modelBuilder.Entity<TagHistory>(entity =>
        {
            entity.ToTable("TagHistory");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TagId, e.Timestamp });
            entity.Property(e => e.Timestamp).HasColumnType("datetime2(3)");
            entity.HasOne(e => e.Tag)
                .WithMany()
                .HasForeignKey(e => e.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Version).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
        });

        modelBuilder.Entity<RecipeParameter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Recipe)
                .WithMany(r => r.Parameters)
                .HasForeignKey(e => e.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Alarm>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.Tag)
                .WithMany()
                .HasForeignKey(e => e.TagId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.History)
                .WithOne(h => h.Alarm)
                .HasForeignKey(h => h.AlarmId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AlarmHistory>(entity =>
        {
            entity.ToTable("AlarmHistory");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Timestamp).HasColumnType("datetime2(3)");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Timestamp).HasColumnType("datetime2(3)");
            entity.Property(e => e.Action).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Details).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<WorkflowRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StartedAtUtc);
            entity.Property(e => e.WorkflowName).HasMaxLength(200);
            entity.Property(e => e.Error).HasMaxLength(2000);
            entity.HasMany(e => e.Steps)
                .WithOne(s => s.Run)
                .HasForeignKey(s => s.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowRunStep>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RunId, e.TimestampUtc });
            entity.Property(e => e.Kind).HasMaxLength(200);
            entity.Property(e => e.Message).HasMaxLength(2000);
            entity.Property(e => e.Error).HasMaxLength(2000);
        });
    }
}
