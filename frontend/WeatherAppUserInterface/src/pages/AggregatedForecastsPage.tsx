import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '../auth/useAuth';
import { account } from '../auth/appwriteClient';
import { LoadingScreen } from '../auth/LoadingScreen';
import { Navbar } from '../components/Navbar';
import { UnauthorizedError } from '../api/profileApi';
import { getAggregatedForecasts } from '../api/aggregatedForecastsApi';
import type { AggregatedForecastItemDto, ServiceAggregationMetadataDto } from '../api/aggregatedForecastsApi';
import { formatLocalTime } from '../utils/formatLocalTime';

interface CityGroup {
  city: string;
  country: string;
  forecasts: AggregatedForecastItemDto[];
  metadata: ServiceAggregationMetadataDto | undefined;
}

// Forecasts already arrive grouped by City, then ordered by Timestamp within each city (UC10) -
// group consecutive runs rather than re-sorting.
function groupByCity(
  forecasts: AggregatedForecastItemDto[],
  serviceMetadata: ServiceAggregationMetadataDto[],
): CityGroup[] {
  const metadataByCity = new Map(serviceMetadata.map((metadata) => [metadata.city, metadata]));
  const groups: CityGroup[] = [];
  for (const forecast of forecasts) {
    let group = groups.at(-1);
    if (!group || group.city !== forecast.city || group.country !== forecast.country) {
      group = { city: forecast.city, country: forecast.country, forecasts: [], metadata: metadataByCity.get(forecast.city) };
      groups.push(group);
    }
    group.forecasts.push(forecast);
  }
  return groups;
}

// UC12 steps 3-6: explains, in plain words, why this city's forecasts come from this service.
function winningServiceNote(metadata: ServiceAggregationMetadataDto): string {
  if (!metadata.aggregationApplicable) {
    // A1: only one of the user's selected services has data for this city.
    return `Only ${metadata.service} has data for this city, so aggregation does not apply.`;
  }
  if (metadata.isUnratedSelection) {
    return `No selected service has enough ratings yet - ${metadata.service} was picked alphabetically.`;
  }
  const rating = `${metadata.averageRating!.toFixed(1)}★ average from ${metadata.ratingCount} rating${metadata.ratingCount === 1 ? '' : 's'}`;
  if (metadata.isTie) {
    return `${metadata.service} is tied for the highest rating (${rating}) and was picked alphabetically.`;
  }
  return `${metadata.service} has the highest rating among your selected services (${rating}).`;
}

// UC10 (View Aggregated Forecast): for each of the user's selected cities, the forecasts from
// whichever selected service is rated highest overall (UC12) - read-only, rating stays on the
// plain forecasts dashboard (UC7).
export function AggregatedForecastsPage() {
  const { logout } = useAuth();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [forecasts, setForecasts] = useState<AggregatedForecastItemDto[]>([]);
  const [serviceMetadata, setServiceMetadata] = useState<ServiceAggregationMetadataDto[]>([]);

  // Session expired mid-page - treat like any other UC1 E2 by logging out.
  const handleAuthFailure = useCallback(
    (err: unknown) => {
      if (!(err instanceof UnauthorizedError)) {
        return false;
      }
      void logout();
      return true;
    },
    [logout],
  );

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const { jwt } = await account.createJWT();
        if (cancelled) {
          return;
        }
        const result = await getAggregatedForecasts(jwt);
        if (cancelled) {
          return;
        }
        setForecasts(result.forecasts);
        setServiceMetadata(result.serviceMetadata);
      } catch (err) {
        if (cancelled) {
          return;
        }
        if (!handleAuthFailure(err)) {
          setLoadError('Could not load your aggregated forecasts. Please retry.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [handleAuthFailure]);

  if (loading) {
    return <LoadingScreen />;
  }

  const groups = groupByCity(forecasts, serviceMetadata);

  return (
    <div className="d-flex flex-column flex-grow-1">
      <Navbar />

      <div className="d-flex justify-content-center flex-grow-1 px-3 pb-4 pb-sm-5">
        <div className="card border-0 w-100" style={{ maxWidth: '56rem' }}>
          <div className="card-body p-4 p-sm-5">
            <div className="mb-4">
              <h1 className="h3 mb-1 heading-blue">Suggested Forecasts</h1>
              <p className="text-muted mb-0">
                For each of your cities, the forecast from your highest-rated selected service.
              </p>
            </div>

            {loadError && (
              <div className="alert alert-danger py-2" role="alert">
                {loadError}
              </div>
            )}

            {!loadError && groups.length === 0 && (
              <p className="text-muted small mb-0">
                No aggregated forecasts yet - add cities and services, or check back once forecast data arrives.
              </p>
            )}

            {groups.map((group) => (
              <section key={`${group.city}|${group.country}`} className="mb-4">
                <h2 className="h5 mb-1 heading-blue">
                  {group.city}, {group.country}
                </h2>
                {group.metadata && <p className="text-muted small mb-2">{winningServiceNote(group.metadata)}</p>}
                <ul className="list-group">
                  {group.forecasts.map((forecast) => (
                    <li
                      key={forecast.forecastId}
                      className="bg-success-subtle text-dark list-group-item d-flex flex-wrap align-items-center gap-2"
                    >
                      <div>
                        <div className="d-flex align-items-center gap-2 flex-wrap">
                          <span className="badge text-bg-secondary">{forecast.type}</span>
                          <span>{formatLocalTime(forecast.timestamp, forecast.offsetMinutes)}</span>
                          {forecast.dangerFlag && <span className="badge text-bg-danger">Danger</span>}
                        </div>
                        <div className="text-muted small mt-1">
                          {forecast.temperature.toFixed(1)}°C · {forecast.humidity.toFixed(0)}% humidity ·{' '}
                          {forecast.windSpeed.toFixed(1)} km/h wind
                        </div>
                      </div>
                    </li>
                  ))}
                </ul>
              </section>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
