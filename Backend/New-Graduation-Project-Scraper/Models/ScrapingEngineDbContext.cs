using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ScraperAPI.Models;

namespace WebApplication1.Models;

public partial class ScrapingEngineDbContext : DbContext
{
    public ScrapingEngineDbContext(DbContextOptions<ScrapingEngineDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<JobQuery> JobQueries { get; set; }

    public virtual DbSet<JobSite> JobSites { get; set; }

    public virtual DbSet<ScrapedJob> ScrapedJobs { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobQuery>(entity =>
        {
            entity.Property(e => e.CreationDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.QjobEndTime).HasDefaultValue(new TimeOnly(17, 0, 0));
            entity.Property(e => e.QjobLocation).HasDefaultValue("Amman, Jordan");
            entity.Property(e => e.QjobName).HasDefaultValue("Software Developer");
            entity.Property(e => e.QjobStartTime).HasDefaultValue(new TimeOnly(9, 0, 0));

            entity.HasOne(d => d.User).WithMany(p => p.JobQueries).HasConstraintName("FK_JobQueries_Users");
        });

        modelBuilder.Entity<JobSite>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<ScrapedJob>(entity =>
        {
            entity.Property(e => e.IsAvailable).HasDefaultValue(true);

            entity.HasOne(d => d.Query).WithMany(p => p.ScrapedJobs).HasConstraintName("FK_ScrapedJobs_JobQueries");

            entity.HasOne(d => d.Site).WithMany(p => p.ScrapedJobs).HasConstraintName("FK_ScrapedJobs_JobSites");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.CreationDate).HasDefaultValueSql("(getdate())");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
