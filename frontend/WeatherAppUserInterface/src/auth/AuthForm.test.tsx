import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ComponentProps, SubmitEvent } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { AuthForm } from './AuthForm';

function renderForm(overrides: Partial<ComponentProps<typeof AuthForm>> = {}) {
  const onSubmit = overrides.onSubmit ?? vi.fn((event: SubmitEvent) => event.preventDefault());
  render(
    <AuthForm
      onSubmit={onSubmit}
      error={null}
      submitting={false}
      submitLabel="Log in"
      submittingLabel="Logging in…"
      {...overrides}
    >
      <input aria-label="Email" />
    </AuthForm>,
  );
  return { onSubmit };
}

describe('AuthForm', () => {
  it('renders its children fields', () => {
    renderForm();

    expect(screen.getByLabelText('Email')).toBeInTheDocument();
  });

  it('shows no alert when error is null', () => {
    renderForm({ error: null });

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows the error message when error is set', () => {
    renderForm({ error: 'Incorrect email or password.' });

    expect(screen.getByText('Incorrect email or password.')).toBeInTheDocument();
  });

  it('shows submitLabel and an enabled button when not submitting', () => {
    renderForm({ submitting: false });

    const button = screen.getByRole('button', { name: 'Log in' });
    expect(button).toBeEnabled();
  });

  it('shows submittingLabel and a disabled button while submitting', () => {
    renderForm({ submitting: true });

    const button = screen.getByRole('button', { name: 'Logging in…' });
    expect(button).toBeDisabled();
  });

  it('calls onSubmit when the form is submitted', async () => {
    const { onSubmit } = renderForm();

    await userEvent.click(screen.getByRole('button', { name: 'Log in' }));

    expect(onSubmit).toHaveBeenCalledTimes(1);
  });
});
