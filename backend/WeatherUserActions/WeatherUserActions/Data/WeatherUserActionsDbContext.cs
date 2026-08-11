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

            modelBuilder.Entity<Forecast>(entity =>
            {
                entity.Property(f => f.Temperature).HasPrecision(3, 1);
                entity.Property(f => f.Humidity).HasPrecision(5, 2);
                entity.Property(f => f.WindSpeed).HasPrecision(5, 2);

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Forecasts_Temperature", "[Temperature] >= -90 AND [Temperature] <= 60");
                    t.HasCheckConstraint("CK_Forecasts_Humidity", "[Humidity] >= 0 AND [Humidity] <= 100");
                    t.HasCheckConstraint("CK_Forecasts_WindSpeed", "[WindSpeed] >= 0 AND [WindSpeed] <= 253");
                });
            });

            modelBuilder.Entity<City>(entity =>
            {
                entity.Property(c => c.Latitude).HasPrecision(11, 8);
                entity.Property(c => c.Longitude).HasPrecision(11, 8);

                entity.HasIndex(c => new { c.Name, c.Country, c.Latitude, c.Longitude })
                    .IsUnique();

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Cities_Latitude", "[Latitude] >= -90 AND [Latitude] <= 90");
                    t.HasCheckConstraint("CK_Cities_Longitude", "[Longitude] >= -180 AND [Longitude] <= 180");
                });
            });
        }
    }
}
