import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AggregatedForecastsPage } from './AggregatedForecastsPage';
import { useAuth } from '../auth/useAuth';
import { account } from '../auth/appwriteClient';
import { getAggregatedForecasts } from '../api/aggregatedForecastsApi';
import type {
  AggregatedForecastItemDto,
  ServiceAggregationMetadataDto,
} from '../api/aggregatedForecastsApi';
import { UnauthorizedError } from '../api/profileApi';

vi.mock('../auth/useAuth', () => ({ useAuth: vi.fn() }));
vi.mock('../auth/appwriteClient', () => ({ account: { createJWT: vi.fn() } }));
vi.mock('../api/aggregatedForecastsApi');
// The real Navbar needs a Router; its own behaviour is covered in Navbar.test.tsx.
vi.mock('../components/Navbar', () => ({ Navbar: () => <nav data-testid="navbar" /> }));

const logoutMock = vi.fn();

function athensForecast(overrides: Partial<AggregatedForecastItemDto> = {}): AggregatedForecastItemDto {
  return {
    forecastId: 1,
    city: 'Athens',
    country: 'Greece',
    service: 'Open-Meteo',
    type: 'CURRENT',
    timestamp: '2026-09-04T12:00:00Z',
    offsetMinutes: 180,
    temperature: 30.4,
    humidity: 45,
    windSpeed: 12,
    dangerFlag: false,
    ...overrides,
  };
}

function athensMetadata(overrides: Partial<ServiceAggregationMetadataDto> = {}): ServiceAggregationMetadataDto {
  return {
    city: 'Athens',
    service: 'Open-Meteo',
    averageRating: 4.5,
    ratingCount: 2,
    aggregationApplicable: true,
    isTie: false,
    isUnratedSelection: false,
    ...overrides,
  };
}

beforeEach(() => {
  vi.mocked(useAuth).mockReturnValue({
    status: 'authenticated',
    hasCitySiteSelection: true,
    profileError: null,
    login: vi.fn(),
    register: vi.fn(),
    logout: logoutMock,
    retryProfileSync: vi.fn(),
  });
  vi.mocked(account.createJWT).mockReset().mockResolvedValue({ jwt: 'jwt-token' } as never);
  vi.mocked(getAggregatedForecasts).mockReset().mockResolvedValue({ forecasts: [], serviceMetadata: [] });
  logoutMock.mockReset();
});

describe('AggregatedForecastsPage - initial load (UC10)', () => {
  it('shows the navbar', async () => {
    render(<AggregatedForecastsPage />);

    expect(await screen.findByTestId('navbar')).toBeInTheDocument();
  });

  it('groups forecasts by city and shows the winning-service note', async () => {
    vi.mocked(getAggregatedForecasts).mockResolvedValue({
      forecasts: [athensForecast()],
      serviceMetadata: [athensMetadata()],
    });

    render(<AggregatedForecastsPage />);

    const section = (await screen.findByRole('heading', { name: 'Athens, Greece' })).closest('section');
    expect(section).toHaveTextContent(
      'Open-Meteo has the highest rating among your selected services (4.5★ average from 2 ratings).',
    );
    expect(section).toHaveTextContent('CURRENT');
    expect(section).toHaveTextContent('30.4°C');
  });

  it('A1: notes when aggregation does not apply because only one service has data', async () => {
    vi.mocked(getAggregatedForecasts).mockResolvedValue({
      forecasts: [athensForecast()],
      serviceMetadata: [athensMetadata({ aggregationApplicable: false, averageRating: null, ratingCount: 0, isUnratedSelection: null })],
    });

    render(<AggregatedForecastsPage />);

    expect(
      await screen.findByText('Only Open-Meteo has data for this city, so aggregation does not apply.'),
    ).toBeInTheDocument();
  });

  it('A2: notes a tie resolved alphabetically', async () => {
    vi.mocked(getAggregatedForecasts).mockResolvedValue({
      forecasts: [athensForecast()],
      serviceMetadata: [athensMetadata({ isTie: true, averageRating: 4 })],
    });

    render(<AggregatedForecastsPage />);

    expect(
      await screen.findByText('Open-Meteo is tied for the highest rating (4.0★ average from 2 ratings) and was picked alphabetically.'),
    ).toBeInTheDocument();
  });

  it('notes an unrated selection resolved alphabetically', async () => {
    vi.mocked(getAggregatedForecasts).mockResolvedValue({
      forecasts: [athensForecast()],
      serviceMetadata: [athensMetadata({ isUnratedSelection: true, averageRating: null, ratingCount: 0 })],
    });

    render(<AggregatedForecastsPage />);

    expect(
      await screen.findByText('No selected service has enough ratings yet - Open-Meteo was picked alphabetically.'),
    ).toBeInTheDocument();
  });

  it('shows an empty-state message when there are no aggregated forecasts', async () => {
    render(<AggregatedForecastsPage />);

    expect(
      await screen.findByText(
        'No aggregated forecasts yet - add cities and services, or check back once forecast data arrives.',
      ),
    ).toBeInTheDocument();
  });

  it('E2: logs out when loading aggregated forecasts is unauthorized', async () => {
    vi.mocked(getAggregatedForecasts).mockRejectedValue(new UnauthorizedError());

    render(<AggregatedForecastsPage />);

    await waitFor(() => expect(logoutMock).toHaveBeenCalled());
  });

  it('shows a load error when the request fails for another reason', async () => {
    vi.mocked(getAggregatedForecasts).mockRejectedValue(new Error('boom'));

    render(<AggregatedForecastsPage />);

    expect(
      await screen.findByText('Could not load your aggregated forecasts. Please retry.'),
    ).toBeInTheDocument();
  });

  it('has no rating widgets - this page is view-only', async () => {
    vi.mocked(getAggregatedForecasts).mockResolvedValue({
      forecasts: [athensForecast()],
      serviceMetadata: [athensMetadata()],
    });

    render(<AggregatedForecastsPage />);
    await screen.findByRole('heading', { name: 'Athens, Greece' });

    expect(screen.queryByRole('button', { name: /Rate \d star/ })).not.toBeInTheDocument();
  });
});
