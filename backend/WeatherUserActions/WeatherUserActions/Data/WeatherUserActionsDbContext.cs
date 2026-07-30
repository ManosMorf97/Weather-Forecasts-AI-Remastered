using Microsoft.EntityFrameworkCore;
using WeatherUserActions.Models;

namespace WeatherUserActions.Data
{
    public class WeatherUserActionsDbContext : DbContext
    {
        public WeatherUserActionsDbContext(DbContextOptions<WeatherUserActionsDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<City> Cities => Set<City>();
        public DbSet<ForecastingService> ForecastingServices => Set<ForecastingService>();
        public DbSet<CitySite> CitySites => Set<CitySite>();
        public DbSet<UserCitySite> UserCitySites => Set<UserCitySite>();
        public DbSet<UserService> UserServices => Set<UserService>();
        public DbSet<Forecast> Forecasts => Set<Forecast>();
        public DbSet<Rating> Ratings => Set<Rating>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AnalyticsReport> AnalyticsReports => Set<AnalyticsReport>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserCitySite>()
                .HasKey(ucs => new { ucs.UserId, ucs.CitySiteId });

            modelBuilder.Entity<UserService>()
                .HasKey(us => new { us.UserId, us.ServiceId });

            modelBuilder.Entity<CitySite>()
                .HasIndex(cs => new { cs.CityId, cs.ServiceId })
                .IsUnique();

            modelBuilder.Entity<Forecast>()
                .HasIndex(f => new { f.CitySiteId, f.Timestamp, f.Type })
                .IsUnique();

            modelBuilder.Entity<Rating>()
                .HasIndex(r => new { r.UserId, r.ForecastId })
                .IsUnique();
        }
    }
}
