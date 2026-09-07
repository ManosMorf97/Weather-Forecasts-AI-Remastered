using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using WeatherUserActions.Data;
using WeatherUserActions.Dtos;

namespace WeatherUserActions.Repositories
{
    public class AggregatedForecastsRepository : IAggregatedForecastsRepository
    {
        private readonly WeatherUserActionsDbContext _db;
        private readonly ILogger<AggregatedForecastsRepository> _logger;

        public AggregatedForecastsRepository(WeatherUserActionsDbContext db, ILogger<AggregatedForecastsRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<(bool Succeeded, List<AggregatedForecastItemDto> Forecasts, List<ServiceAggregationMetadataDto> ServiceMetadata)>
            TryGetAggregatedForecastsAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;

                // One round trip. City/Service are joined straight away - CitySite.City and
                // CitySite.Service are single-valued FK references, not collections, so there's no
                // fan-out risk in joining them. HasAnyForecastData/HasUpcomingForecastData/
                // RatingCount/AverageRating are correlated subqueries per CitySite instead: Forecasts
                // and Ratings *are* one-to-many off CitySite, so joining those directly would fan out
                // against each other. Ratings are never time-filtered - a service's average rating is
                // an all-time signal of its reliability, independent of which forecast is upcoming.
                var candidates = await _db.UserCitySites
                    .Where(userCitySite => userCitySite.UserId == userId)
                    .Select(userCitySite => new CitySiteCandidate(
                        userCitySite.CitySite.CitySiteId,
                        userCitySite.CitySite.CityId,
                        userCitySite.CitySite.City.Name,
                        userCitySite.CitySite.City.Country,
                        userCitySite.CitySite.Service.Name,
                        userCitySite.CitySite.Forecasts.Any(),
                        userCitySite.CitySite.Forecasts.Any(forecast => forecast.Timestamp >= now),
                        userCitySite.CitySite.Forecasts.SelectMany(forecast => forecast.Ratings).Count(),
                        userCitySite.CitySite.Forecasts.SelectMany(forecast => forecast.Ratings).Average(rating => (decimal?)rating.Value)))
                    .ToListAsync(cancellationToken);

                if (candidates.Count == 0)
                {
                    return (true, [], []);
                }

                // Pick every city's winner first (pure in-memory work over `candidates`), then fetch
                // all of their upcoming forecasts in one batched query - a flat cap of two DB round
                // trips total, instead of one extra round trip per returned city.
                var winners = new List<(CitySiteCandidate Winner, bool AggregationApplicable, bool IsTie, bool? IsUnratedSelection)>();
                //CLAUDE. WHy do not order by all columns of city
                var cityGroups = candidates
                    .GroupBy(candidate => candidate.CityId)
                    .OrderBy(group => group.First().CityName, StringComparer.Ordinal);

                foreach (var cityGroup in cityGroups)
                {
                    var cityCandidates = cityGroup.Where(candidate => candidate.HasAnyForecastData).ToList();
                    if (cityCandidates.Count == 0)
                    {
                        // UC12 E1 (per city): none of the user's selected services have any forecast data at all.
                        continue;
                    }

                    var (winner, aggregationApplicable, isTie, isUnratedSelection) = PickWinner(cityCandidates);
                    if (winner is null)
                    {
                        // Every candidate has forecast/rating history but nothing upcoming right now.
                        continue;
                    }

                    winners.Add((winner, aggregationApplicable, isTie, isUnratedSelection));
                }

                if (winners.Count == 0)
                {
                    return (true, [], []);
                }

                var winningCitySiteIds = winners.Select(entry => entry.Winner.CitySiteId).ToList();
                var forecastsByCitySiteId = (await _db.Forecasts
                    .Where(forecast => winningCitySiteIds.Contains(forecast.CitySiteId) && forecast.Timestamp >= now)
                    .OrderBy(forecast => forecast.Timestamp)
                    .ToListAsync(cancellationToken))
                    .ToLookup(forecast => forecast.CitySiteId);

                var forecasts = new List<AggregatedForecastItemDto>();
                var serviceMetadata = new List<ServiceAggregationMetadataDto>();

                foreach (var (winner, aggregationApplicable, isTie, isUnratedSelection) in winners)
                {
                    forecasts.AddRange(forecastsByCitySiteId[winner.CitySiteId].Select(forecast => new AggregatedForecastItemDto(
                        forecast.ForecastId,
                        winner.CityName,
                        winner.CountryName,
                        winner.ServiceName,
                        forecast.Type,
                        forecast.Timestamp,
                        forecast.OffsetMinutes,
                        forecast.Temperature,
                        forecast.Humidity,
                        forecast.WindSpeed,
                        forecast.DangerFlag)));

                    serviceMetadata.Add(new ServiceAggregationMetadataDto(
                        winner.CityName,
                        winner.ServiceName,
                        winner.AverageRating,
                        winner.RatingCount,
                        aggregationApplicable,
                        isTie,
                        isUnratedSelection));
                }

                return (true, forecasts, serviceMetadata);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to load aggregated forecasts for {UserId}", userId);
                return (false, [], []);
            }
        }

        // UC12 steps 3-6 / A1 / A2, extended with a fallback: the winner must actually have an
        // upcoming forecast to show, so candidates without one are excluded before rating even
        // enters the picture. AggregationApplicable reflects whether more than one service was ever
        // in play at all (based on the full candidate list, before the upcoming-data filter) - so a
        // top-rated-but-stale service still counts as a real competitor. IsTie/IsUnratedSelection,
        // by contrast, are scoped to only the currently-displayable candidates: a service tied on
        // rating but with no upcoming forecast doesn't count toward IsTie, since it was never in
        // contention for actually being shown. Returns a null winner only if no candidate has
        // upcoming data at all. IsUnratedSelection is null (rather than false) whenever there was no
        // real comparison to begin with - a single service, or no displayable candidate at all - so
        // callers can tell "picked confidently" apart from "there was nothing to compare".
        private static (CitySiteCandidate? Winner, bool AggregationApplicable, bool IsTie, bool? IsUnratedSelection) PickWinner(
            List<CitySiteCandidate> candidates)
        {
            var aggregationApplicable = candidates.Count > 1;

            if (!aggregationApplicable)
            {
                var only = candidates[0];
                return (only.HasUpcomingForecastData ? only : null, AggregationApplicable: false, IsTie: false, IsUnratedSelection: null);
            }

            var displayableCandidates = candidates.Where(candidate => candidate.HasUpcomingForecastData).ToList();
            if (displayableCandidates.Count == 0)
            {
                return (null, AggregationApplicable: true, IsTie: false, IsUnratedSelection: null);
            }

            var eligible = displayableCandidates.Where(candidate => candidate.RatingCount >= 2).ToList();

            if (eligible.Count == 0)
            {
                var unratedWinner = displayableCandidates.OrderBy(candidate => candidate.ServiceName, StringComparer.Ordinal).First();
                return (unratedWinner, AggregationApplicable: true, IsTie: false, IsUnratedSelection: true);
            }

            var maxAverageRating = eligible.Max(candidate => candidate.AverageRating);
            var topCandidates = eligible
                .Where(candidate => candidate.AverageRating == maxAverageRating)
                .OrderBy(candidate => candidate.ServiceName, StringComparer.Ordinal)
                .ToList();

            return (topCandidates[0], AggregationApplicable: true, IsTie: topCandidates.Count > 1, IsUnratedSelection: false);
        }

        private sealed record CitySiteCandidate(
            int CitySiteId,
            int CityId,
            string CityName,
            string CountryName,
            string ServiceName,
            bool HasAnyForecastData,
            bool HasUpcomingForecastData,
            int RatingCount,
            decimal? AverageRating);
    }
}
