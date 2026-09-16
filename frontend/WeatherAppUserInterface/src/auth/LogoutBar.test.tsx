import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LogoutBar } from './LogoutBar';
import { useAuth } from './useAuth';

vi.mock('./useAuth', () => ({ useAuth: vi.fn() }));

const logoutMock = vi.fn();

beforeEach(() => {
  logoutMock.mockReset();
  vi.mocked(useAuth).mockReturnValue({
    status: 'authenticated',
    hasCitySiteSelection: true,
    profileError: null,
    login: vi.fn(),
    register: vi.fn(),
    logout: logoutMock,
    retryProfileSync: vi.fn(),
  });
});

describe('LogoutBar', () => {
  it('calls logout when clicked', async () => {
    render(<LogoutBar />);

    await userEvent.click(screen.getByRole('button', { name: 'Log out' }));

    expect(logoutMock).toHaveBeenCalledTimes(1);
  });
});
