import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Navbar } from './Navbar';
import { useAuth } from '../auth/useAuth';

vi.mock('../auth/useAuth', () => ({ useAuth: vi.fn() }));

const logoutMock = vi.fn();

function mockAuth(hasCitySiteSelection: boolean | null) {
  vi.mocked(useAuth).mockReturnValue({
    status: 'authenticated',
    hasCitySiteSelection,
    profileError: null,
    login: vi.fn(),
    register: vi.fn(),
    logout: logoutMock,
    retryProfileSync: vi.fn(),
  });
}

function renderNavbar(path = '/dashboard') {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Navbar />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  logoutMock.mockReset();
  mockAuth(true);
});

describe('Navbar - user with a CitySite selection', () => {
  it('shows the Current predictions and Set Selections links', () => {
    renderNavbar();

    expect(screen.getByRole('link', { name: 'Current predictions' })).toHaveAttribute('href', '/dashboard');
    expect(screen.getByRole('link', { name: 'Set Selections' })).toHaveAttribute('href', '/setup');
  });

  it('marks the link of the current page as active', () => {
    renderNavbar('/setup');

    expect(screen.getByRole('link', { name: 'Current predictions' })).toHaveAttribute('href', '/dashboard');
    expect(screen.getByRole('link', { name: 'Set Selections' })).toHaveAttribute('href', '/setup');
    expect(screen.getByRole('link', { name: 'Set Selections' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Current predictions' })).not.toHaveAttribute('aria-current');
  });

  it('still shows Log out', async () => {
    renderNavbar();

    await userEvent.click(screen.getByRole('button', { name: 'Log out' }));

    expect(logoutMock).toHaveBeenCalledTimes(1);
  });
});

describe('Navbar - user without a CitySite selection', () => {
  it('hides the links but keeps Log out', () => {
    mockAuth(false);

    renderNavbar('/setup');

    expect(screen.queryByRole('navigation', { name: 'Main' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Current predictions' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Set Selections' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Log out' })).toBeInTheDocument();
  });

  it('hides the links while the profile state is unknown', () => {
    mockAuth(null);

    renderNavbar('/setup');

    expect(screen.queryByRole('navigation', { name: 'Main' })).not.toBeInTheDocument();
  });
});

