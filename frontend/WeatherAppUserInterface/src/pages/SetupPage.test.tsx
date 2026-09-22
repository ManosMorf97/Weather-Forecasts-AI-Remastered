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
import { saveServices } from '../api/userServicesApi';


vi.mock('../auth/useAuth', () => ({ useAuth: vi.fn() }));
vi.mock('../auth/appwriteClient', () => ({ account: { createJWT: vi.fn() } }));
vi.mock('../api/selectionsApi');
vi.mock('../api/cityApi');

vi.mock('../api/userServicesApi');


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

  vi.mocked(saveServices).mockReset().mockResolvedValue(undefined);

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

  it('hides the navbar links when the user has no CitySite selection yet', async () => {
    renderSetupPage();
    await screen.findByText('Open-Meteo');

    expect(screen.queryByRole('link', { name: 'Current predictions' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Suggested Forecasts' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Set Selections' })).not.toBeInTheDocument();
  });

  it('shows the navbar links when the user already has a CitySite selection', async () => {
    vi.mocked(useAuth).mockReturnValue({
      status: 'authenticated',
      hasCitySiteSelection: true,
      profileError: null,
      login: vi.fn(),
      register: vi.fn(),
      logout: logoutMock,
      retryProfileSync: retryProfileSyncMock,
    });

    renderSetupPage();
    await screen.findByText('Open-Meteo');

    expect(screen.getByRole('link', { name: 'Current predictions' })).toHaveAttribute('href', '/dashboard');
    expect(screen.getByRole('link', { name: 'Suggested Forecasts' })).toHaveAttribute('href', '/aggregated');
    expect(screen.getByRole('link', { name: 'Set Selections' })).toHaveAttribute('href', '/setup');
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

    // 2s search debounce - wait past it rather than the default 1s findBy timeout.
    const picker = await screen.findByRole(
      'combobox',
      { name: 'Add a city from the search results' },
      { timeout: 3000 },
    );
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

    // 2s search debounce - wait past it rather than the default 1s findBy timeout.
    expect(
      await screen.findByText('No results. Try a different search.', {}, { timeout: 3000 }),
    ).toBeInTheDocument();
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
    // 2s search debounce - wait past it rather than the default 1s findBy timeout.
    const picker = await screen.findByRole(
      'combobox',
      { name: 'Add a city from the search results' },
      { timeout: 3000 },
    );
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

describe('SetupPage - City API down with no cities selected (services only)', () => {
  // 2s search debounce - wait past it rather than the default 1s findBy timeout.
  async function triggerCitySearchFailure(user: ReturnType<typeof userEvent.setup>) {
    vi.mocked(searchCities).mockRejectedValue(new Error('City search failed'));
    await user.type(screen.getByLabelText('Search for a city'), 'Athens');
    await screen.findByText('Could not search cities right now. Please try again.', {}, { timeout: 3000 });
  }

  it('enables confirm with only a service selected and saves it through saveServices', async () => {
    const user = userEvent.setup();
    renderSetupPage();
    await screen.findByText('Open-Meteo');
    const confirmButton = screen.getByRole('button', { name: 'Confirm selection' });

    await triggerCitySearchFailure(user);
    expect(confirmButton).toBeDisabled();
    expect(screen.getByText('Select at least one service to continue.')).toBeInTheDocument();
    expect(
      screen.getByText(
        'There is a problem with loading cities right now. You can save your services and come back later to choose your cities.',
      ),
    ).toBeInTheDocument();

    await user.click(screen.getByRole('checkbox', { name: 'Open-Meteo' }));
    expect(confirmButton).toBeEnabled();

    await act(() => user.click(confirmButton));

    expect(saveServices).toHaveBeenCalledWith('jwt-token', [1]);
    expect(saveSelections).not.toHaveBeenCalled();
    expect(
      await screen.findByText('Your services are saved. Please come back later to choose your cities.'),
    ).toBeInTheDocument();
    // The "problem with cities" warning is replaced by the saved notice.
    expect(screen.queryByText(/There is a problem with loading cities/)).not.toBeInTheDocument();
    // No CitySite selection yet, so navigating home would only bounce back to /setup.
    expect(retryProfileSyncMock).not.toHaveBeenCalled();
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('shows an error and no success notice when saving the services fails', async () => {
    vi.mocked(saveServices).mockRejectedValue(new Error('Could not persist the pending service selection.'));
    const user = userEvent.setup();
    renderSetupPage();
    await screen.findByText('Open-Meteo');
    await triggerCitySearchFailure(user);
    await user.click(screen.getByRole('checkbox', { name: 'Open-Meteo' }));

    await act(() => user.click(screen.getByRole('button', { name: 'Confirm selection' })));

    expect(await screen.findByText('Could not persist the pending service selection.')).toBeInTheDocument();
    expect(screen.queryByText(/Your services are saved/)).not.toBeInTheDocument();
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('E2: logs out when saving the services is unauthorized', async () => {
    vi.mocked(saveServices).mockRejectedValue(new UnauthorizedError());
    const user = userEvent.setup();
    renderSetupPage();
    await screen.findByText('Open-Meteo');
    await triggerCitySearchFailure(user);
    await user.click(screen.getByRole('checkbox', { name: 'Open-Meteo' }));

    await act(() => user.click(screen.getByRole('button', { name: 'Confirm selection' })));

    await waitFor(() => expect(logoutMock).toHaveBeenCalled());
  });

  it('still saves through saveSelections when a city is already selected', async () => {
    vi.mocked(getSelections).mockResolvedValue({
      services: [{ serviceId: 1, name: 'Open-Meteo', selected: true }],
      cities: [{ name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }],
    });
    const user = userEvent.setup();
    renderSetupPage();
    await screen.findByText('Athens, Greece');
    await triggerCitySearchFailure(user);

    // A city is already selected, so the "come back later" warning must not appear.
    expect(screen.queryByText(/There is a problem with loading cities/)).not.toBeInTheDocument();

    await act(() => user.click(screen.getByRole('button', { name: 'Confirm selection' })));

    expect(saveSelections).toHaveBeenCalledWith(
      'jwt-token',
      [{ name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }],
      [1],
    );
    expect(saveServices).not.toHaveBeenCalled();
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/', { replace: true }));
  });
});
