import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { AuthField } from './AuthField';

describe('AuthField', () => {
  it('associates the label with the input via id', () => {
    render(
      <AuthField id="login-email" label="Email" type="email" autoComplete="email" value="" onChange={vi.fn()} />,
    );

    expect(screen.getByLabelText('Email')).toBe(screen.getByRole('textbox'));
  });

  it('renders the given type, autoComplete, required, and value', () => {
    render(
      <AuthField
        id="register-password"
        label="Password"
        type="password"
        autoComplete="new-password"
        minLength={8}
        value="hunter2"
        onChange={vi.fn()}
      />,
    );

    const input = screen.getByLabelText('Password');
    expect(input).toHaveAttribute('type', 'password');
    expect(input).toHaveAttribute('autocomplete', 'new-password');
    expect(input).toHaveAttribute('minlength', '8');
    expect(input).toBeRequired();
    expect(input).toHaveValue('hunter2');
  });

  it('calls onChange with the typed value on every keystroke', async () => {
    const onChange = vi.fn();
    render(
      <AuthField id="login-email" label="Email" type="email" autoComplete="email" value="" onChange={onChange} />,
    );

    await userEvent.type(screen.getByLabelText('Email'), 'a');

    expect(onChange).toHaveBeenCalledWith('a');
  });
});
