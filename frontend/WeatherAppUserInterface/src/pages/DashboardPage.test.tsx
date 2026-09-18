import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DashboardPage } from './DashboardPage';
import { useAuth } from '../auth/useAuth';
import { account } from '../auth/appwriteClient';
import { getForecasts, rateForecast, removeRating } from '../api/forecastsApi';
import type { ForecastItemDto } from '../api/forecastsApi';
import { UnauthorizedError } from '../api/profileApi';

vi.mock('../auth/useAuth', () => ({ useAuth: vi.fn() }));
vi.mock('../auth/appwriteClient', () => ({ account: { createJWT: vi.fn() } }));
vi.mock('../api/forecastsApi');

const logoutMock = vi.fn();

function athensCurrent(overrides: Partial<ForecastItemDto> = {}): ForecastItemDto {
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
    userRating: null,
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
  vi.mocked(getForecasts).mockReset().mockResolvedValue([]);
  vi.mocked(rateForecast).mockReset().mockResolvedValue(undefined);
  vi.mocked(removeRating).mockReset().mockResolvedValue(undefined);
  logoutMock.mockReset();
});

describe('DashboardPage - initial load (UC6)', () => {
  it('groups forecasts by service, then city', async () => {
    vi.mocked(getForecasts).mockResolvedValue([
      athensCurrent(),
      { ...athensCurrent(), forecastId: 2, city: 'Rome', country: 'Italy', type: 'HOURLY' },
      { ...athensCurrent(), forecastId: 3, service: 'WeatherAPI' },
    ]);

    render(<DashboardPage />);

    const openMeteoSection = (await screen.findByRole('heading', { name: 'Open-Meteo' })).closest('section');
    expect(openMeteoSection).toHaveTextContent('Athens, Greece');
    expect(openMeteoSection).toHaveTextContent('Rome, Italy');

    const weatherApiSection = screen.getByRole('heading', { name: 'WeatherAPI' }).closest('section');
    expect(weatherApiSection).toHaveTextContent('Athens, Greece');
  });

  it('A2: shows an empty-state message when there are no forecasts', async () => {
    vi.mocked(getForecasts).mockResolvedValue([]);

    render(<DashboardPage />);

    expect(
      await screen.findByText('No forecasts yet for your saved cities and services.'),
    ).toBeInTheDocument();
  });

  it('E2: logs out when loading forecasts is unauthorized', async () => {
    vi.mocked(getForecasts).mockRejectedValue(new UnauthorizedError());

    render(<DashboardPage />);

    await waitFor(() => expect(logoutMock).toHaveBeenCalled());
  });
});

describe('DashboardPage - rating (UC7)', () => {
  it('rates a forecast and reflects it in the stars', async () => {
    vi.mocked(getForecasts).mockResolvedValue([athensCurrent()]);
    const user = userEvent.setup();
    render(<DashboardPage />);
    await screen.findByRole('heading', { name: 'Athens, Greece' });

    await user.click(screen.getByRole('button', { name: 'Rate 4 stars' }));

    await waitFor(() => expect(rateForecast).toHaveBeenCalledWith('jwt-token', 1, 4));
    expect(await screen.findByRole('button', { name: 'Remove rating' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rate 4 stars' })).toHaveTextContent('★');
    expect(screen.getByRole('button', { name: 'Rate 5 stars' })).toHaveTextContent('☆');
  });

  it('A2: clears a rating', async () => {
    vi.mocked(getForecasts).mockResolvedValue([athensCurrent({ userRating: 3 })]);
    const user = userEvent.setup();
    render(<DashboardPage />);
    const clearButton = await screen.findByRole('button', { name: 'Remove rating' });

    await user.click(clearButton);

    await waitFor(() => expect(removeRating).toHaveBeenCalledWith('jwt-token', 1));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Remove rating' })).not.toBeInTheDocument());
  });

  it('shows an inline error next to the row when saving a rating fails', async () => {
    vi.mocked(getForecasts).mockResolvedValue([athensCurrent()]);
    vi.mocked(rateForecast).mockRejectedValue(new Error('Could not save your rating. Please retry.'));
    const user = userEvent.setup();
    render(<DashboardPage />);
    await screen.findByRole('heading', { name: 'Athens, Greece' });

    await user.click(screen.getByRole('button', { name: 'Rate 2 stars' }));

    expect(await screen.findByText('Could not save your rating. Please retry.')).toBeInTheDocument();
  });
});
