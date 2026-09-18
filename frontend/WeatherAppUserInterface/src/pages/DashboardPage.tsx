import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '../auth/useAuth';
import { account } from '../auth/appwriteClient';
import { LoadingScreen } from '../auth/LoadingScreen';
import { LogoutBar } from '../auth/LogoutBar';
import { UnauthorizedError } from '../api/profileApi';
import { getForecasts, rateForecast, removeRating } from '../api/forecastsApi';
import type { ForecastItemDto } from '../api/forecastsApi';
import { StarRating } from '../components/StarRating';

interface CityGroup {
  city: string;
  country: string;
  forecasts: ForecastItemDto[];
}

interface ServiceGroup {
  service: string;
  cities: CityGroup[];
}

// Forecasts already arrive sorted by Service, then City, then Timestamp (UC6) - group
// consecutive runs rather than re-sorting.
function groupForecasts(forecasts: ForecastItemDto[]): ServiceGroup[] {
  const groups: ServiceGroup[] = [];
  for (const forecast of forecasts) {
    let serviceGroup = groups.at(-1);
    if (!serviceGroup || serviceGroup.service !== forecast.service) {
      serviceGroup = { service: forecast.service, cities: [] };
      groups.push(serviceGroup);
    }
    let cityGroup = serviceGroup.cities.at(-1);
    if (!cityGroup || cityGroup.city !== forecast.city || cityGroup.country !== forecast.country) {
      cityGroup = { city: forecast.city, country: forecast.country, forecasts: [] };
      serviceGroup.cities.push(cityGroup);
    }
    cityGroup.forecasts.push(forecast);
  }
  return groups;
}

// Formats a UTC timestamp as that city's own local wall-clock time (via its offsetMinutes),
// not the browser's local timezone.
function formatLocalTime(timestamp: string, offsetMinutes: number): string {
  const local = new Date(new Date(timestamp).getTime() + offsetMinutes * 60_000);
  const date = local.toLocaleDateString(undefined, { timeZone: 'UTC', month: 'short', day: 'numeric' });
  const time = local.toLocaleTimeString(undefined, { timeZone: 'UTC', hour: '2-digit', minute: '2-digit' });
  return `${date}, ${time}`;
}

// UC2 step 7: landing spot for an already-configured user. UC6 (View Forecasts) + UC7 (Rate).
export function DashboardPage() {
  const { logout } = useAuth();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [forecasts, setForecasts] = useState<ForecastItemDto[]>([]);
  const [rowErrors, setRowErrors] = useState<Record<number, string>>({});
  const [pendingRatingIds, setPendingRatingIds] = useState<Set<number>>(new Set());

  // Session expired mid-dashboard - treat like any other UC1 E2 by logging out.
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
        const result = await getForecasts(jwt);
        if (cancelled) {
          return;
        }
        setForecasts(result);
      } catch (err) {
        if (cancelled) {
          return;
        }
        if (!handleAuthFailure(err)) {
          setLoadError('Could not load your forecasts. Please retry.');
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

  function clearRowError(forecastId: number) {
    setRowErrors((current) => {
      const next = { ...current };
      delete next[forecastId];
      return next;
    });
  }

  async function handleRate(forecastId: number, value: number) {
    setPendingRatingIds((current) => new Set(current).add(forecastId));
    clearRowError(forecastId);
    try {
      const { jwt } = await account.createJWT();
      await rateForecast(jwt, forecastId, value);
      setForecasts((current) =>
        current.map((forecast) =>
          forecast.forecastId === forecastId ? { ...forecast, userRating: value } : forecast,
        ),
      );
    } catch (err) {
      if (!handleAuthFailure(err)) {
        setRowErrors((current) => ({
          ...current,
          [forecastId]: err instanceof Error ? err.message : 'Could not save your rating. Please retry.',
        }));
      }
    } finally {
      setPendingRatingIds((current) => {
        const next = new Set(current);
        next.delete(forecastId);
        return next;
      });
    }
  }

  async function handleClearRating(forecastId: number) {
    setPendingRatingIds((current) => new Set(current).add(forecastId));
    clearRowError(forecastId);
    try {
      const { jwt } = await account.createJWT();
      await removeRating(jwt, forecastId);
      setForecasts((current) =>
        current.map((forecast) =>
          forecast.forecastId === forecastId ? { ...forecast, userRating: null } : forecast,
        ),
      );
    } catch (err) {
      if (!handleAuthFailure(err)) {
        setRowErrors((current) => ({
          ...current,
          [forecastId]: err instanceof Error ? err.message : 'Could not remove your rating. Please retry.',
        }));
      }
    } finally {
      setPendingRatingIds((current) => {
        const next = new Set(current);
        next.delete(forecastId);
        return next;
      });
    }
  }

  if (loading) {
    return <LoadingScreen />;
  }

  const groups = groupForecasts(forecasts);

  return (
    <div className="d-flex flex-column flex-grow-1">
      <LogoutBar />

      <div className="d-flex justify-content-center flex-grow-1 px-3 pb-4 pb-sm-5">
        <div className="card border-0 w-100" style={{ maxWidth: '56rem' }}>
          <div className="card-body p-4 p-sm-5">
            <div className="mb-4">
              <h1 className="h3 mb-1">Your forecasts</h1>
              <p className="text-muted mb-0">Current and upcoming forecasts for your saved cities and services.</p>
            </div>

            {loadError && (
              <div className="alert alert-danger py-2" role="alert">
                {loadError}
              </div>
            )}

            {!loadError && forecasts.length === 0 && (
              <p className="text-muted small mb-0">No forecasts yet for your saved cities and services.</p>
            )}

            {groups.map((serviceGroup) => (
              <section key={serviceGroup.service} className="mb-4">
                <h2 className="h5 mb-3">{serviceGroup.service}</h2>
                {serviceGroup.cities.map((cityGroup) => (
                  <div key={`${cityGroup.city}|${cityGroup.country}`} className="mb-3">
                    <h3 className="h6 mb-2">
                      {cityGroup.city}, {cityGroup.country}
                    </h3>
                    <ul className="list-group">
                      {cityGroup.forecasts.map((forecast) => (
                        <li
                          key={forecast.forecastId}
                          className="list-group-item d-flex flex-wrap align-items-center justify-content-between gap-2"
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
                            {rowErrors[forecast.forecastId] && (
                              <div className="text-danger small mt-1">{rowErrors[forecast.forecastId]}</div>
                            )}
                          </div>
                          <StarRating
                            value={forecast.userRating}
                            disabled={pendingRatingIds.has(forecast.forecastId)}
                            onRate={(value) => void handleRate(forecast.forecastId, value)}
                            onClear={() => void handleClearRating(forecast.forecastId)}
                          />
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </section>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
