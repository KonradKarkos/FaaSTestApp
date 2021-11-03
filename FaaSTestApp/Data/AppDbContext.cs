using FaaSTestApp.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace FaaSTestApp.Data
{
    public class AppDbContext : DbContext
    {
        public virtual DbSet<TestRequest> Requests { get; set; }
        public virtual DbSet<TestResult> Results { get; set; }
        public virtual DbSet<TestSession> Sessions { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                @"");
        }
        public override int SaveChanges()
        {
            var entries = ChangeTracker.Entries().Where(e => e.Entity is BaseEntity && e.State == EntityState.Added);

            foreach(var entityEntry in entries)
            {
                ((BaseEntity)entityEntry.Entity).CreatedAt = DateTime.UtcNow;
            }

            return base.SaveChanges();
        }
    }
}
