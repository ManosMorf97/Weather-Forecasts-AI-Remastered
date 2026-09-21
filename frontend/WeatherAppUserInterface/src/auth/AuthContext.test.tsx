import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from './AuthContext';
import { useAuth } from './useAuth';
import { account } from './appwriteClient';
import { createProfile, UnauthorizedError } from '../api/profileApi';
import { readCache, writeCache } from '../api/apiCache';

vi.mock('./appwriteClient', () => ({
  account: {
    get: vi.fn(),
    createJWT: vi.fn(),
    createEmailPasswordSession: vi.fn(),
    create: vi.fn(),
    deleteSession: vi.fn(),
  },
}));

vi.mock('../api/profileApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/profileApi')>();
  return { ...actual, createProfile: vi.fn() };
});

// Exposes useAuth()'s state/actions as DOM text/buttons so tests can assert and trigger them.
function AuthProbe() {
  const auth = useAuth();
  return (
    <div>
      <span data-testid="status">{auth.status}</span>
      <span data-testid="hasCitySiteSelection">{String(auth.hasCitySiteSelection)}</span>
      <span data-testid="profileError">{auth.profileError ?? ''}</span>
      <button onClick={() => void auth.login('user@example.com', 'password123')}>login</button>
      <button onClick={() => void auth.register('User', 'user@example.com', 'password123')}>register</button>
      <button onClick={() => void auth.logout()}>logout</button>
    </div>
  );
}

function renderAuth() {
  render(
    <AuthProvider>
      <AuthProbe />
    </AuthProvider>,
  );
}
beforeEach(() => {
  vi.mocked(account.get).mockReset().mockRejectedValue(new Error('no session'));
  vi.mocked(account.createJWT).mockReset().mockResolvedValue({ jwt: 'fake-jwt' } as never);
  vi.mocked(account.createEmailPasswordSession).mockReset().mockResolvedValue({} as never);
  vi.mocked(account.create).mockReset().mockResolvedValue({} as never);
  vi.mocked(account.deleteSession).mockReset().mockResolvedValue({} as never);
  vi.mocked(createProfile).mockReset();
  localStorage.clear();
});

describe('AuthProvider - initial mount (UC1 restore session)', () => {
  it('is unauthenticated when there is no existing session', async () => {
    renderAuth();

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated'));
    expect(screen.getByTestId('hasCitySiteSelection')).toHaveTextContent('null');
    expect(account.createEmailPasswordSession).not.toHaveBeenCalled();
    expect(account.create).not.toHaveBeenCalled();
  });

  it('is authenticated and reports the profile state when a session already exists', async () => {
    vi.mocked(account.get).mockResolvedValue({} as never);
    vi.mocked(createProfile).mockResolvedValue({ hasCitySiteSelection: true });

    renderAuth();

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));
    expect(screen.getByTestId('hasCitySiteSelection')).toHaveTextContent('true');
    expect(account.createEmailPasswordSession).not.toHaveBeenCalled();
    expect(account.create).not.toHaveBeenCalled();
  });
});

describe('AuthProvider - login (UC1)', () => {
  it('becomes authenticated on successful login', async () => {
    vi.mocked(createProfile).mockResolvedValue({ hasCitySiteSelection: false });

    renderAuth();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated'));

    await act(() => userEvent.click(screen.getByText('login')));

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));
    expect(account.createEmailPasswordSession).toHaveBeenCalledWith({
      email: 'user@example.com',
      password: 'password123',
    });
    expect(account.create).not.toHaveBeenCalled();
  });

  it('E2: falls back to unauthenticated when the backend rejects the JWT', async () => {
    vi.mocked(createProfile).mockRejectedValue(new UnauthorizedError());
    renderAuth();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated'));

    await act(() => userEvent.click(screen.getByText('login')));

    await waitFor(() => expect(screen.getByTestId('hasCitySiteSelection')).toHaveTextContent('null'));
    expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated');
    expect(account.createEmailPasswordSession).toHaveBeenCalledWith({
      email: 'user@example.com',
      password: 'password123',
    });
    expect(account.create).not.toHaveBeenCalled();
  });

  it('E1: stays authenticated with a profileError when profile provisioning fails', async () => {
    vi.mocked(createProfile).mockRejectedValue(new Error('db is down'));
    renderAuth();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated'));

    await act(() => userEvent.click(screen.getByText('login')));

    await waitFor(() =>
      expect(screen.getByTestId('profileError')).toHaveTextContent('Could not load your profile. Please retry.'),
    );
    expect(screen.getByTestId('status')).toHaveTextContent('authenticated');
    expect(account.createEmailPasswordSession).toHaveBeenCalledWith({
      email: 'user@example.com',
      password: 'password123',
    });
    expect(account.create).not.toHaveBeenCalled();
  });
});

describe('AuthProvider - register (UC1 sign up)', () => {
  it('creates the account, starts a session, and syncs the profile', async () => {
    vi.mocked(createProfile).mockResolvedValue({ hasCitySiteSelection: false });
    renderAuth();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated'));

    await act(() => userEvent.click(screen.getByText('register')));

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));
    expect(account.create).toHaveBeenCalledWith(
      expect.objectContaining({ email: 'user@example.com', password: 'password123', name: 'User' }),
    );
    expect(account.createEmailPasswordSession).toHaveBeenCalledWith({
      email: 'user@example.com',
      password: 'password123',
    });
  });
});

describe('AuthProvider - logout (UC14)', () => {
  it('clears the session and resets profile state', async () => {
    vi.mocked(account.get).mockResolvedValue({} as never);
    vi.mocked(createProfile).mockResolvedValue({ hasCitySiteSelection: true });
    renderAuth();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));

    await act(() => userEvent.click(screen.getByText('logout')));

    expect(account.deleteSession).toHaveBeenCalledWith({ sessionId: 'current' });
    expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated');
    expect(screen.getByTestId('hasCitySiteSelection')).toHaveTextContent('null');
    expect(account.createEmailPasswordSession).not.toHaveBeenCalled();
    expect(account.create).not.toHaveBeenCalled();
  });
});

describe('AuthProvider - API cache is cleared so one user never sees another user\'s data', () => {
  function seedCache() {
    writeCache('selections', { services: [], cities: [] });
    writeCache('forecasts', []);
  }

  function expectCacheEmpty() {
    expect(readCache('selections')).toBeNull();
    expect(readCache('forecasts')).toBeNull();
  }

  it('on mount when there is no existing session', async () => {
    seedCache();

    renderAuth();

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated'));
    expectCacheEmpty();
  });

  it('keeps the cache on mount when the session is restored', async () => {
    seedCache();
    vi.mocked(account.get).mockResolvedValue({} as never);
    vi.mocked(createProfile).mockResolvedValue({ hasCitySiteSelection: true });

    renderAuth();

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));
    expect(readCache('selections')).toEqual({ services: [], cities: [] });
    expect(readCache('forecasts')).toEqual([]);
  });

  it('on login', async () => {
    vi.mocked(createProfile).mockResolvedValue({ hasCitySiteSelection: false });
    renderAuth();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated'));
    seedCache();

    await act(() => userEvent.click(screen.getByText('login')));

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));
    expectCacheEmpty();
  });

  it('on register', async () => {
    vi.mocked(createProfile).mockResolvedValue({ hasCitySiteSelection: false });
    renderAuth();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated'));
    seedCache();

    await act(() => userEvent.click(screen.getByText('register')));

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));
    expectCacheEmpty();
  });

  it('on logout', async () => {
    vi.mocked(account.get).mockResolvedValue({} as never);
    vi.mocked(createProfile).mockResolvedValue({ hasCitySiteSelection: true });
    renderAuth();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));
    seedCache();

    await act(() => userEvent.click(screen.getByText('logout')));

    expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated');
    expectCacheEmpty();
  });

  it('when the backend rejects the JWT', async () => {
    vi.mocked(account.get).mockResolvedValue({} as never);
    vi.mocked(createProfile).mockRejectedValue(new UnauthorizedError());
    seedCache();

    renderAuth();

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated'));
    expectCacheEmpty();
  });
});
