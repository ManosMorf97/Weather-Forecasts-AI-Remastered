import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AnalyticsPage } from './AnalyticsPage';
import { useAuth } from '../auth/useAuth';
import { account } from '../auth/appwriteClient';
import { getSelections } from '../api/selectionsApi';
import type { Selections } from '../api/selectionsApi';
import { requestAnalytics } from '../api/analyticsApi';
import { UnauthorizedError } from '../api/profileApi';

vi.mock('../auth/useAuth', () => ({ useAuth: vi.fn() }));
vi.mock('../auth/appwriteClient', () => ({ account: { createJWT: vi.fn() } }));
vi.mock('../api/selectionsApi');
vi.mock('../api/analyticsApi');
// The real Navbar needs a Router; its own behaviour is covered in Navbar.test.tsx.
vi.mock('../components/Navbar', () => ({ Navbar: () => <nav data-testid="navbar" /> }));

const logoutMock = vi.fn();

function sampleSelections(overrides: Partial<Selections> = {}): Selections {
  return {
    services: [
      { serviceId: 1, name: 'Open-Meteo', selected: true },
      { serviceId: 2, name: 'WeatherAPI', selected: false },
    ],
    cities: [{ cityId: 10, name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }],
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
  vi.mocked(getSelections).mockReset().mockResolvedValue(sampleSelections());
  vi.mocked(requestAnalytics).mockReset().mockResolvedValue('batch-1');
  logoutMock.mockReset();
});

describe('AnalyticsPage - initial load (UC8)', () => {
  it('shows the navbar', async () => {
    render(<AnalyticsPage />);

    expect(await screen.findByTestId('navbar')).toBeInTheDocument();
  });

  it('lists the user selected cities and only the selected services', async () => {
    render(<AnalyticsPage />);

    expect(await screen.findByLabelText('Athens, Greece')).toBeInTheDocument();
    expect(screen.getByLabelText('Open-Meteo')).toBeInTheDocument();
    expect(screen.queryByLabelText('WeatherAPI')).not.toBeInTheDocument();
  });

  it('E2: logs out when loading selections is unauthorized', async () => {
    vi.mocked(getSelections).mockRejectedValue(new UnauthorizedError());

    render(<AnalyticsPage />);

    await waitFor(() => expect(logoutMock).toHaveBeenCalled());
  });

  it('shows a load error when the request fails for another reason', async () => {
    vi.mocked(getSelections).mockRejectedValue(new Error('boom'));

    render(<AnalyticsPage />);

    expect(await screen.findByText('Could not load your cities and services. Please retry.')).toBeInTheDocument();
  });
});

describe('AnalyticsPage - submitting (UC8 step 4-6)', () => {
  async function fillValidForm() {
    await userEvent.click(await screen.findByLabelText('Athens, Greece'));
    await userEvent.click(screen.getByLabelText('Open-Meteo'));
    await userEvent.type(screen.getByLabelText('Start date'), '2026-01-01');
    await userEvent.type(screen.getByLabelText('End date'), '2026-01-31');
  }

  it('disables submit until a city, a service, and both dates are picked', async () => {
    render(<AnalyticsPage />);

    expect(await screen.findByRole('button', { name: 'Request analytics' })).toBeDisabled();

    await fillValidForm();

    expect(screen.getByRole('button', { name: 'Request analytics' })).toBeEnabled();
  });

  it('A1: disables submit and shows an inline error when start is after end', async () => {
    render(<AnalyticsPage />);
    await userEvent.click(await screen.findByLabelText('Athens, Greece'));
    await userEvent.click(screen.getByLabelText('Open-Meteo'));
    await userEvent.type(screen.getByLabelText('Start date'), '2026-02-01');
    await userEvent.type(screen.getByLabelText('End date'), '2026-01-01');

    expect(screen.getByText('Start date must be on or before the end date.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Request analytics' })).toBeDisabled();
  });

  it('A1: disables submit and shows an inline error when the range exceeds 366 days', async () => {
    render(<AnalyticsPage />);
    await userEvent.click(await screen.findByLabelText('Athens, Greece'));
    await userEvent.click(screen.getByLabelText('Open-Meteo'));
    await userEvent.type(screen.getByLabelText('Start date'), '2025-01-01');
    await userEvent.type(screen.getByLabelText('End date'), '2026-01-03');

    expect(screen.getByText('The date range must not exceed 366 days.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Request analytics' })).toBeDisabled();
  });

  it('submits the picked cityIds, serviceIds and date range', async () => {
    render(<AnalyticsPage />);
    await fillValidForm();

    await userEvent.click(screen.getByRole('button', { name: 'Request analytics' }));

    await waitFor(() =>
      expect(requestAnalytics).toHaveBeenCalledWith('jwt-token', [10], [1], '2026-01-01', '2026-01-31'),
    );
  });

  it('shows a success banner with the queued confirmation', async () => {
    render(<AnalyticsPage />);
    await fillValidForm();

    await userEvent.click(screen.getByRole('button', { name: 'Request analytics' }));

    expect(
      await screen.findByText('Report queued. You will receive it by email once it is ready.'),
    ).toBeInTheDocument();
  });

  it('A1: surfaces a validation error from the backend', async () => {
    vi.mocked(requestAnalytics).mockRejectedValue(
      new Error('Start must be on or before end, and the range must not exceed 366 days.'),
    );
    render(<AnalyticsPage />);
    await fillValidForm();

    await userEvent.click(screen.getByRole('button', { name: 'Request analytics' }));

    expect(
      await screen.findByText('Start must be on or before end, and the range must not exceed 366 days.'),
    ).toBeInTheDocument();
  });

  it('E2: logs out when submitting is unauthorized', async () => {
    vi.mocked(requestAnalytics).mockRejectedValue(new UnauthorizedError());
    render(<AnalyticsPage />);
    await fillValidForm();

    await userEvent.click(screen.getByRole('button', { name: 'Request analytics' }));

    await waitFor(() => expect(logoutMock).toHaveBeenCalled());
  });
});
