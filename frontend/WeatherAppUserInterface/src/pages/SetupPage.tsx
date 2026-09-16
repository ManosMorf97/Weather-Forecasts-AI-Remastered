import { useCallback, useEffect, useState } from 'react';
import type { SubmitEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { account } from '../auth/appwriteClient';
import { LoadingScreen } from '../auth/LoadingScreen';
import { LogoutBar } from '../auth/LogoutBar';
import { UnauthorizedError } from '../api/profileApi';
import { searchCities } from '../api/cityApi';
import type { GeocodedCity } from '../api/cityApi';
import { getSelections, saveSelections } from '../api/selectionsApi';
import type { CityDto, ServiceSelectionDto } from '../api/selectionsApi';

function citySignature(city: CityDto | GeocodedCity): string {
  return `${city.name}|${city.country}|${city.latitude}|${city.longitude}`;
}

// UC4 (Select Cities) + UC5 (Select Forecasting Services): initial setup landing spot for a
// user with no CitySite selection yet (UC2 step 6).
export function SetupPage() {
  const { logout, retryProfileSync } = useAuth();
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [services, setServices] = useState<ServiceSelectionDto[]>([]);
  // Dictionaries keyed by id/signature - presence in the dictionary means "selected".
  const [selectedServiceIds, setSelectedServiceIds] = useState<Record<number, true>>({});
  const [selectedCities, setSelectedCities] = useState<Record<string, CityDto>>({});

  const [query, setQuery] = useState('');
  const [searchResults, setSearchResults] = useState<GeocodedCity[]>([]);
  const [searching, setSearching] = useState(false);
  const [searched, setSearched] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const [saveError, setSaveError] = useState<string | null>(null);

  // Session expired mid-setup - treat like any other UC1 E2 by logging out; returns true if handled.
  const handleAuthFailure = useCallback(
    (err: unknown) => {
      if (!(err instanceof UnauthorizedError)) 
        return false;
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
        const selections = await getSelections(jwt);
        if (cancelled) {
          return;
        }
        setServices(selections.services);
        setSelectedServiceIds(
          Object.fromEntries(
            selections.services.filter((service) => service.selected).map((service) => [service.serviceId, true]),
          ),
        );
        setSelectedCities(Object.fromEntries(selections.cities.map((city) => [citySignature(city), city])));
      } catch (err) {
        if (cancelled) {
          return;
        }
        if (!handleAuthFailure(err)) {
          setLoadError('Could not load your current selections. Please retry.');
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

  async function handleSearch(event: SubmitEvent) {
    event.preventDefault();
    const trimmed = query.trim();
    if (!trimmed) {
      return;
    }

    setSearching(true);
    setSearchError(null);
    setSearchResults([]);
    try {
      const results = await searchCities(trimmed);
      setSearchResults(results);
      setSearched(true);
    } catch {
      // E1: searchCities already retried once internally.
      setSearchError('Could not search cities right now. Please try again.');
    } finally {
      setSearching(false);
    }
  }

  function addCity(city: GeocodedCity) {
    setSelectedCities((current) => ({ ...current, [citySignature(city)]: city }));
  }

  function removeCity(city: CityDto) {
    setSelectedCities((current) => {
      const next = { ...current };
      delete next[citySignature(city)];
      return next;
    });
  }

  function toggleService(serviceId: number) {
    setSelectedServiceIds((current) => {
      const next = { ...current };
      if (next[serviceId]) {
        delete next[serviceId];
      } else {
        next[serviceId] = true;
      }
      return next;
    });
  }

  const selectedCityList = Object.values(selectedCities);
  const selectedServiceIdList = Object.keys(selectedServiceIds).map(Number);

  // A3/A2: at least one city and one service must remain selected to confirm.
  const canConfirm = selectedCityList.length > 0 && selectedServiceIdList.length > 0;

  async function handleConfirm() {
    if (!canConfirm) {
      return;
    }

    setLoading(true);
    setSaveError(null);
    try {
      const { jwt } = await account.createJWT();
      await saveSelections(jwt, selectedCityList, selectedServiceIdList);
      await retryProfileSync();
      navigate('/', { replace: true });
    } catch (err) {
      if (!handleAuthFailure(err)) {
        setSaveError(err instanceof Error ? err.message : 'Could not save your selections. Please retry.');
      }
      setLoading(false);
    }
  }

  if (loading) {
    return <LoadingScreen />;
  }

  return (
    <div className="d-flex flex-column flex-grow-1">
      <LogoutBar />

      <div className="d-flex justify-content-center flex-grow-1 px-3 pb-4 pb-sm-5">
        <div className="card border-0 w-100" style={{ maxWidth: '48rem' }}>
          <div className="card-body p-4 p-sm-5">
            <div className="mb-4">
              <h1 className="h3 mb-1">Let&apos;s set things up</h1>
              <p className="text-muted mb-0">Pick the cities and forecasting services you want to track.</p>
            </div>

            {loadError && (
              <div className="alert alert-danger py-2" role="alert">
                {loadError}
              </div>
            )}

            <section className="mb-4">
              <h2 className="h5 mb-3">Cities</h2>

              <form className="d-flex flex-wrap gap-2 mb-3" onSubmit={(event) => void handleSearch(event)}>
                <input
                  type="text"
                  className="form-control flex-grow-1"
                  style={{ minWidth: '12rem' }}
                  placeholder="Search for a city…"
                  aria-label="Search for a city"
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                />
                <button className="btn btn-primary" type="submit" disabled={searching || !query.trim()}>
                  {searching ? 'Searching…' : 'Search'}
                </button>
              </form>

              {searchError && (
                <div className="alert alert-danger py-2" role="alert">
                  {searchError}
                </div>
              )}

              {searched && !searchError && searchResults.length === 0 && (
                <p className="text-muted small">No results. Try a different search.</p>
              )}

              {searchResults.length > 0 && (
                <ul className="list-group mb-3">
                  {searchResults.map((city) => {
                    const alreadyAdded = Boolean(selectedCities[citySignature(city)]);
                    return (
                      <li
                        key={citySignature(city)}
                        className="list-group-item d-flex justify-content-between align-items-center flex-wrap gap-2"
                      >
                        <span>
                          {city.name}, {city.country}
                        </span>
                        <button
                          type="button"
                          className="btn btn-sm btn-outline-primary"
                          disabled={alreadyAdded}
                          onClick={() => addCity(city)}
                        >
                          {alreadyAdded ? 'Added' : 'Add'}
                        </button>
                      </li>
                    );
                  })}
                </ul>
              )}

              <h3 className="h6 mb-2">Selected cities</h3>
              {selectedCityList.length === 0 ? (
                <p className="text-muted small mb-0">No cities selected yet.</p>
              ) : (
                <ul className="list-group">
                  {selectedCityList.map((city) => (
                    <li
                      key={citySignature(city)}
                      className="list-group-item d-flex justify-content-between align-items-center flex-wrap gap-2"
                    >
                      <span>
                        {city.name}, {city.country}
                      </span>
                      <button
                        type="button"
                        className="btn btn-sm btn-outline-danger"
                        onClick={() => removeCity(city)}
                      >
                        Remove
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            <section className="mb-4">
              <h2 className="h5 mb-3">Forecasting services</h2>
              {services.length === 0 ? (
                <p className="text-muted small mb-0">No forecasting services are available right now.</p>
              ) : (
                <div className="d-flex flex-column gap-2">
                  {services.map((service) => (
                    <div className="form-check" key={service.serviceId}>
                      <input
                        className="form-check-input"
                        type="checkbox"
                        id={`service-${service.serviceId}`}
                        checked={Boolean(selectedServiceIds[service.serviceId])}
                        onChange={() => toggleService(service.serviceId)}
                      />
                      <label className="form-check-label" htmlFor={`service-${service.serviceId}`}>
                        {service.name}
                      </label>
                    </div>
                  ))}
                </div>
              )}
            </section>

            {saveError && (
              <div className="alert alert-danger py-2" role="alert">
                {saveError}
              </div>
            )}

            {!canConfirm && (
              <p className="text-muted small">Select at least one city and one service to continue.</p>
            )}

            <button
              className="btn btn-primary w-100"
              type="button"
              disabled={!canConfirm}
              onClick={() => void handleConfirm()}
            >
              Confirm selection
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
