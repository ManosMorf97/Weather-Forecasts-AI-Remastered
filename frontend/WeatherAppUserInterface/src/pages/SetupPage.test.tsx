import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SetupPage } from './SetupPage';
import { useAuth } from '../auth/useAuth';
import { account } from '../auth/appwriteClient';
import { getSelections, saveSelections } from '../api/selectionsApi';
import { searchCities } from '../api/cityApi';
import { UnauthorizedError } from '../api/profileApi';

vi.mock('../auth/useAuth', () => ({ useAuth: vi.fn() }));
vi.mock('../auth/appwriteClient', () => ({ account: { createJWT: vi.fn() } }));
vi.mock('../api/selectionsApi');
vi.mock('../api/cityApi');

const navigateMock = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigateMock };
});

const logoutMock = vi.fn();
const retryProfileSyncMock = vi.fn();

function renderSetupPage() {
  render(
    <MemoryRouter>
      <SetupPage />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.mocked(useAuth).mockReturnValue({
    status: 'authenticated',
    hasCitySiteSelection: false,
    profileError: null,
    login: vi.fn(),
    register: vi.fn(),
    logout: logoutMock,
    retryProfileSync: retryProfileSyncMock,
  });
  vi.mocked(account.createJWT).mockReset().mockResolvedValue({ jwt: 'jwt-token' } as never);
  vi.mocked(getSelections)
    .mockReset()
    .mockResolvedValue({
      services: [
        { serviceId: 1, name: 'Open-Meteo', selected: false },
        { serviceId: 2, name: 'OpenWeatherMap', selected: false },
      ],
      cities: [],
    });
  vi.mocked(saveSelections).mockReset().mockResolvedValue(undefined);
  vi.mocked(searchCities).mockReset();
  logoutMock.mockReset();
  retryProfileSyncMock.mockReset().mockResolvedValue(undefined);
  navigateMock.mockReset();
});

describe('SetupPage - initial load', () => {
  it('shows the available services and no cities selected yet', async () => {
    renderSetupPage();

    expect(await screen.findByText('Open-Meteo')).toBeInTheDocument();
    expect(screen.getByText('OpenWeatherMap')).toBeInTheDocument();
    expect(screen.getByText('No cities selected yet.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Confirm selection' })).toBeDisabled();
  });

  it('E2: logs out when loading selections is unauthorized', async () => {
    vi.mocked(getSelections).mockRejectedValue(new UnauthorizedError());

    renderSetupPage();

    await waitFor(() => expect(logoutMock).toHaveBeenCalled());
  });
});

describe('SetupPage - city search (UC4)', () => {
  it('adds a searched city to the selected list', async () => {
    vi.mocked(searchCities).mockResolvedValue([
      { name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 },
    ]);
    const user = userEvent.setup();
    renderSetupPage();
    await screen.findByText('Open-Meteo');

    await user.type(screen.getByLabelText('Search for a city'), 'Athens');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    const picker = await screen.findByRole('combobox', { name: 'Add a city from the search results' });
    await user.selectOptions(picker, 'Athens, Greece');

    // Already-added result disappears from the picker, and now only the placeholder remains.
    expect(picker).toHaveTextContent('Choose a city to add…');
    expect(screen.getByText('All results are already added.')).toBeInTheDocument();

    // Selected-cities row: Athens now has its own "Remove" button.
    const selectedCityRow = screen.getByRole('button', { name: 'Remove Athens, Greece' }).closest('li');
    expect(selectedCityRow).toHaveTextContent('Athens, Greece');

    expect(screen.queryByText('No cities selected yet.')).not.toBeInTheDocument();
  });

  it('A1: shows a message when there are no results', async () => {
    vi.mocked(searchCities).mockResolvedValue([]);
    const user = userEvent.setup();
    renderSetupPage();
    await screen.findByText('Open-Meteo');

    await user.type(screen.getByLabelText('Search for a city'), 'zzzzz');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    expect(await screen.findByText('No results. Try a different search.')).toBeInTheDocument();
  });

  it('removes a selected city', async () => {
    vi.mocked(getSelections).mockResolvedValue({
      services: [{ serviceId: 1, name: 'Open-Meteo', selected: true }],
      cities: [{ name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }],
    });
    const user = userEvent.setup();
    renderSetupPage();

    await screen.findByText('Athens, Greece');
    await user.click(screen.getByRole('button', { name: 'Remove Athens, Greece' }));

    expect(screen.getByText('No cities selected yet.')).toBeInTheDocument();
  });
});

describe('SetupPage - confirm (UC4 + UC5)', () => {
  it('is disabled until a city and a service are both selected', async () => {
    vi.mocked(searchCities).mockResolvedValue([
      { name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 },
    ]);
    const user = userEvent.setup();
    renderSetupPage();
    await screen.findByText('Open-Meteo');

    const confirmButton = screen.getByRole('button', { name: 'Confirm selection' });
    expect(confirmButton).toBeDisabled();

    await user.click(screen.getByRole('checkbox', { name: 'Open-Meteo' }));
    expect(confirmButton).toBeDisabled();

    await user.type(screen.getByLabelText('Search for a city'), 'Athens');
    await user.click(screen.getByRole('button', { name: 'Search' }));
    const picker = await screen.findByRole('combobox', { name: 'Add a city from the search results' });
    await user.selectOptions(picker, 'Athens, Greece');

    expect(confirmButton).toBeEnabled();

    await user.click(screen.getByRole('button', { name: 'Remove Athens, Greece' }));
    expect(confirmButton).toBeDisabled();
  });

  it('saves the selection, refreshes the profile, and navigates home', async () => {
    vi.mocked(getSelections).mockResolvedValue({
      services: [{ serviceId: 1, name: 'Open-Meteo', selected: false }],
      cities: [{ name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }],
    });
    const user = userEvent.setup();
    renderSetupPage();
    await screen.findByText('Athens, Greece');

    await user.click(screen.getByRole('checkbox', { name: 'Open-Meteo' }));
    await act(() => user.click(screen.getByRole('button', { name: 'Confirm selection' })));

    expect(saveSelections).toHaveBeenCalledWith(
      'jwt-token',
      [{ name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }],
      [1],
    );
    await waitFor(() => expect(retryProfileSyncMock).toHaveBeenCalled());
    expect(navigateMock).toHaveBeenCalledWith('/', { replace: true });
  });

  it('shows an error and does not navigate when saving fails', async () => {
    vi.mocked(getSelections).mockResolvedValue({
      services: [{ serviceId: 1, name: 'Open-Meteo', selected: true }],
      cities: [{ name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }],
    });
    vi.mocked(saveSelections).mockRejectedValue(new Error('Could not persist the city/service selection.'));
    const user = userEvent.setup();
    renderSetupPage();
    await screen.findByText('Athens, Greece');

    await act(() => user.click(screen.getByRole('button', { name: 'Confirm selection' })));

    expect(await screen.findByText('Could not persist the city/service selection.')).toBeInTheDocument();
    expect(navigateMock).not.toHaveBeenCalled();
  });
});
