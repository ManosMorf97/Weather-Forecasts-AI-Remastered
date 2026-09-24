import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { account } from '../auth/appwriteClient';
import { LoadingScreen } from '../auth/LoadingScreen';
import { Navbar } from '../components/Navbar';
import { UnauthorizedError } from '../api/profileApi';
import { searchCities } from '../api/cityApi';
import type { GeocodedCity } from '../api/cityApi';
import { getSelections, saveSelections } from '../api/selectionsApi';
import { saveServices } from '../api/userServicesApi';
import type { CityDto, ServiceSelectionDto } from '../api/selectionsApi';

function citySignature(city: CityDto | GeocodedCity): string {
  return `${city.name}|${city.country}|${city.latitude}|${city.longitude}`;
}

const SEARCH_DEBOUNCE_MS = 2000;
const MIN_QUERY_LENGTH = 2;

// UC4 (Select Cities) + UC5 (Select Forecasting Services): initial setup landing spot for a
// user with no CitySite selection yet (UC2 step 6).
export function SetupPage() {
  const { logout, retryProfileSync } = useAuth();
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // services carries its own `selected` flag per entry - toggled in place, filtered at submit.
  const [services, setServices] = useState<ServiceSelectionDto[]>([]);
  // Dictionary keyed by signature - presence means "selected".
  const [selectedCities, setSelectedCities] = useState<Record<string, CityDto>>({});

  const [query, setQuery] = useState('');
  const [searchResults, setSearchResults] = useState<GeocodedCity[]>([]);
  const [searching, setSearching] = useState(false);
  const [searched, setSearched] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const [saveError, setSaveError] = useState<string | null>(null);
  const [servicesSavedNotice, setServicesSavedNotice] = useState(false);

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

  // UC4 step 3: searches as the user types, debounced so we don't fire a request per keystroke.
  useEffect(() => {
    const trimmed = query.trim();
    let cancelled = false;

    const timeoutId = window.setTimeout(() => {
      if (trimmed.length < MIN_QUERY_LENGTH) {
        setSearchResults([]);
        setSearched(false);
        setSearchError(null);
        setSearching(false);
        return;
      }

      setSearching(true);
      setSearchError(null);

      (async () => {
        try {
          const results = await searchCities(trimmed);
          if (cancelled) {
            return;
          }
          setSearchResults(results);
          setSearched(true);
        } catch {
          // E1: searchCities already retried once internally.
          if (!cancelled) {
            setSearchError('Could not search cities right now. Please try again.');
          }
        } finally {
          if (!cancelled) {
            setSearching(false);
          }
        }
      })();
    }, SEARCH_DEBOUNCE_MS);

    return () => {
      cancelled = true;
      window.clearTimeout(timeoutId);
    };
  }, [query]);

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
    setServices((current) =>
      current.map((service) =>
        service.serviceId === serviceId ? { ...service, selected: !service.selected } : service,
      ),
    );
  }

  // Loaded cities carry a cityId (SelectedCityDto); newly searched ones don't (GeocodedCity).
  // Save only the CityDto shape the backend's SaveSelectionsRequest expects.
  const selectedCityList: CityDto[] = Object.values(selectedCities).map(
    ({ name, country, latitude, longitude }) => ({ name, country, latitude, longitude }),
  );
  const selectedServiceIdList = services.filter((service) => service.selected).map((service) => service.serviceId);
  const availableSearchResults = searchResults.filter((city) => !selectedCities[citySignature(city)]);

  // UC4 E1: the City API is down and the user has no city to fall back on, so only the
  // services can be saved for now.
  const servicesOnly = selectedCityList.length === 0 && searchError !== null;

  // A3/A2: at least one service must remain selected, plus at least one city - unless the
  // City API is down and the user has no cities (servicesOnly).
  const canConfirm = selectedServiceIdList.length > 0 && (selectedCityList.length > 0 || servicesOnly);

  async function handleConfirm() {
    if (!canConfirm) {
      return;
    }

    setLoading(true);
    setSaveError(null);
    setServicesSavedNotice(false);
    try {
      const { jwt } = await account.createJWT();
      if (servicesOnly) {
        // No city yet, so the profile still has no CitySite selection - stay here rather than
        // navigating, or "/" would just redirect back to this page.
        await saveServices(jwt, selectedServiceIdList);
        setServicesSavedNotice(true);
        setLoading(false);
        return;
      }
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
      <Navbar />

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

              <input
                type="text"
                className="form-control mb-2"
                placeholder="Search for a city…"
                aria-label="Search for a city"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />

              {searching && <p className="text-muted small">Searching…</p>}

              {searchError && (
                <div className="alert alert-danger py-2" role="alert">
                  {searchError}
                </div>
              )}

              {searched && !searchError && searchResults.length === 0 && (
                <p className="text-muted small">No results. Try a different search.</p>
              )}

              {searchResults.length > 0 &&
                (availableSearchResults.length > 0 ? (
                  <select
                    className="form-select mb-3"
                    aria-label="Add a city from the search results"
                    value=""
                    onChange={(event) => {
                      const city = availableSearchResults.find(
                        (result) => citySignature(result) === event.target.value,
                      );
                      if (city) {
                        addCity(city);
                      }
                    }}
                  >
                    <option value="" disabled>
                      Choose a city to add…
                    </option>
                    {availableSearchResults.map((city) => (
                      <option key={citySignature(city)} value={citySignature(city)}>
                        {city.name}, {city.country}
                      </option>
                    ))}
                  </select>
                ) : (
                  <p className="text-muted small mb-3">All results are already added.</p>
                ))
              }

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
                        className="btn btn-sm btn-outline-danger rounded-circle d-flex align-items-center justify-content-center p-0"
                        style={{ width: '1.75rem', height: '1.75rem' }}
                        aria-label={`Remove ${city.name}, ${city.country}`}
                        onClick={() => removeCity(city)}
                      >
                        <span aria-hidden="true">×</span>
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
                        checked={service.selected}
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

            {servicesSavedNotice && (
              <div className="alert alert-success py-2" role="status">
                Your services are saved. Please come back later to choose your cities.
              </div>
            )}

            {servicesOnly && !servicesSavedNotice && (
              <div className="alert alert-warning py-2" role="alert">
                There is a problem with loading cities right now. You can save your services and come back
                later to choose your cities.
              </div>
            )}

            {!canConfirm && (
              <p className="text-muted small">
                {servicesOnly
                  ? 'Select at least one service to continue.'
                  : 'Select at least one city and one service to continue.'}
              </p>
            )}

            <button
              className="btn btn-info w-100"
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
