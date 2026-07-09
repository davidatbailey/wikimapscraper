using Microsoft.EntityFrameworkCore;
using WikiMapScraper.Web.Models;

namespace WikiMapScraper.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<TopicPlace> TopicPlaces => Set<TopicPlace>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Topic>()
            .HasIndex(t => t.Name)
            .IsUnique();

        modelBuilder.Entity<Place>()
            .HasIndex(p => p.WikiPageId)
            .IsUnique();

        modelBuilder.Entity<TopicPlace>()
            .HasIndex(tp => new { tp.TopicId, tp.PlaceId })
            .IsUnique();

        modelBuilder.Entity<TopicPlace>()
            .HasOne(tp => tp.Topic)
            .WithMany(t => t.TopicPlaces)
            .HasForeignKey(tp => tp.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TopicPlace>()
            .HasOne(tp => tp.Place)
            .WithMany(p => p.TopicPlaces)
            .HasForeignKey(tp => tp.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
