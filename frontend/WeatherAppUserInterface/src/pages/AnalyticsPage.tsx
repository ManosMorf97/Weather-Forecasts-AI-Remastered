import { useCallback, useEffect, useState } from 'react';
import type { MouseEvent } from 'react';
import { useAuth } from '../auth/useAuth';
import { account } from '../auth/appwriteClient';
import { LoadingScreen } from '../auth/LoadingScreen';
import { Navbar } from '../components/Navbar';
import { UnauthorizedError } from '../api/profileApi';
import { getSelections } from '../api/selectionsApi';
import type { SelectedCityDto, ServiceSelectionDto } from '../api/selectionsApi';
import { requestAnalytics } from '../api/analyticsApi';

// UC8 A1: the backend rejects a range over 366 days - mirrored here so the user finds out
// before submitting rather than from the 400 response.
const MAX_DATE_RANGE_DAYS = 366;

// Browsers only open the calendar from the tiny icon by default; open it on any click in the box.
function openDatePicker(event: MouseEvent<HTMLInputElement>) {
  try {
    event.currentTarget.showPicker?.();
  } catch {
    // showPicker can throw when the browser refuses (e.g. already open) - the native controls still work.
  }
}

function dateRangeErrorFor(start: string, end: string): string | null {
  if (!start || !end) {
    return null;
  }
  if (start > end) {
    return 'Start date must be on or before the end date.';
  }
  const days = Math.round((Date.parse(end) - Date.parse(start)) / 86_400_000);
  if (days > MAX_DATE_RANGE_DAYS) {
    return `The date range must not exceed ${MAX_DATE_RANGE_DAYS} days.`;
  }
  return null;
}

// UC8 (Request Analytics): the user picks a subset of their own selected cities and services,
// plus a date range, and submits. Generation and delivery are asynchronous - the finished
// report (PDF with temperature/humidity/wind stats + danger-day counts) is emailed later, so
// this page only confirms the request was queued; there is no preview or status polling.
export function AnalyticsPage() {
  const { logout } = useAuth();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [cities, setCities] = useState<SelectedCityDto[]>([]);
  const [services, setServices] = useState<ServiceSelectionDto[]>([]);

  const [selectedCityIds, setSelectedCityIds] = useState<Set<number>>(new Set());
  const [selectedServiceIds, setSelectedServiceIds] = useState<Set<number>>(new Set());
  const [dateRangeStart, setDateRangeStart] = useState('');
  const [dateRangeEnd, setDateRangeEnd] = useState('');

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [batchId, setBatchId] = useState<string | null>(null);

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
        const selections = await getSelections(jwt);
        if (cancelled) {
          return;
        }
        setCities(selections.cities);
        setServices(selections.services.filter((service) => service.selected));
      } catch (err) {
        if (cancelled) {
          return;
        }
        if (!handleAuthFailure(err)) {
          setLoadError('Could not load your cities and services. Please retry.');
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

  function toggleCity(cityId: number) {
    setSelectedCityIds((current) => {
      const next = new Set(current);
      if (next.has(cityId)) {
        next.delete(cityId);
      } else {
        next.add(cityId);
      }
      return next;
    });
  }

  function toggleService(serviceId: number) {
    setSelectedServiceIds((current) => {
      const next = new Set(current);
      if (next.has(serviceId)) {
        next.delete(serviceId);
      } else {
        next.add(serviceId);
      }
      return next;
    });
  }

  const dateRangeError = dateRangeErrorFor(dateRangeStart, dateRangeEnd);

  const canSubmit =
    selectedCityIds.size > 0 &&
    selectedServiceIds.size > 0 &&
    dateRangeStart !== '' &&
    dateRangeEnd !== '' &&
    dateRangeError === null;

  async function handleSubmit() {
    if (!canSubmit) {
      return;
    }

    setSubmitting(true);
    setSubmitError(null);
    setBatchId(null);
    try {
      const { jwt } = await account.createJWT();
      const result = await requestAnalytics(
        jwt,
        [...selectedCityIds],
        [...selectedServiceIds],
        dateRangeStart,
        dateRangeEnd,
      );
      setBatchId(result);
    } catch (err) {
      if (!handleAuthFailure(err)) {
        setSubmitError(err instanceof Error ? err.message : 'Could not request the analytics report. Please retry.');
      }
    } finally {
      setSubmitting(false);
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
              <h1 className="h3 mb-1 heading-orange">Request Analytics</h1>
              <p className="text-muted mb-0">
                Pick cities, services and a date range. The finished report is emailed to you as a PDF.
              </p>
            </div>

            {loadError && (
              <div className="alert alert-danger py-2" role="alert">
                {loadError}
              </div>
            )}

            <section className="mb-4">
              <h2 className="h5 mb-3">Cities</h2>
              {cities.length === 0 ? (
                <p className="text-muted small mb-0">No cities selected yet.</p>
              ) : (
                <div className="d-flex flex-column gap-2">
                  {cities.map((city) => (
                    <div className="form-check" key={city.cityId}>
                      <input
                        className="form-check-input"
                        type="checkbox"
                        id={`analytics-city-${city.cityId}`}
                        checked={selectedCityIds.has(city.cityId)}
                        onChange={() => toggleCity(city.cityId)}
                      />
                      <label className="form-check-label" htmlFor={`analytics-city-${city.cityId}`}>
                        {city.name}, {city.country}
                      </label>
                    </div>
                  ))}
                </div>
              )}
            </section>

            <section className="mb-4">
              <h2 className="h5 mb-3">Forecasting services</h2>
              {services.length === 0 ? (
                <p className="text-muted small mb-0">No forecasting services selected yet.</p>
              ) : (
                <div className="d-flex flex-column gap-2">
                  {services.map((service) => (
                    <div className="form-check" key={service.serviceId}>
                      <input
                        className="form-check-input"
                        type="checkbox"
                        id={`analytics-service-${service.serviceId}`}
                        checked={selectedServiceIds.has(service.serviceId)}
                        onChange={() => toggleService(service.serviceId)}
                      />
                      <label className="form-check-label" htmlFor={`analytics-service-${service.serviceId}`}>
                        {service.name}
                      </label>
                    </div>
                  ))}
                </div>
              )}
            </section>

            <section className="mb-4">
              <h2 className="h5 mb-1">Date range</h2>
              <p className="text-muted small mb-3">Click a date box to open the calendar and pick the first and last day to include.</p>
              <div className="row g-2">
                <div className="col-sm-6">
                  <label className="form-label fw-semibold" htmlFor="analytics-date-start">
                    Start date
                  </label>
                  <input
                    id="analytics-date-start"
                    type="date"
                    className="form-control form-control-lg"
                    value={dateRangeStart}
                    onClick={openDatePicker}
                    onChange={(event) => setDateRangeStart(event.target.value)}
                  />
                </div>
                <div className="col-sm-6">
                  <label className="form-label fw-semibold" htmlFor="analytics-date-end">
                    End date
                  </label>
                  <input
                    id="analytics-date-end"
                    type="date"
                    className="form-control form-control-lg"
                    value={dateRangeEnd}
                    onClick={openDatePicker}
                    onChange={(event) => setDateRangeEnd(event.target.value)}
                  />
                </div>
              </div>
              {dateRangeError && <p className="text-danger small mb-0 mt-2">{dateRangeError}</p>}
            </section>

            {submitError && (
              <div className="alert alert-danger py-2" role="alert">
                {submitError}
              </div>
            )}

            {batchId && (
              <div className="alert alert-success py-2" role="status">
                Report queued. You will receive it by email once it is ready.
              </div>
            )}

            {!canSubmit && dateRangeError === null && (
              <p className="text-muted small">Select at least one city, one service, and a date range.</p>
            )}

            <button
              className="btn btn-info w-100"
              type="button"
              disabled={!canSubmit || submitting}
              onClick={() => void handleSubmit()}
            >
              {submitting && <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true" />}
              {submitting ? 'Requesting…' : 'Request analytics'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
