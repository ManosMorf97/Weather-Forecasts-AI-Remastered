import { useState } from 'react';
import type { SubmitEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AppwriteException } from 'appwrite';
import { useAuth } from '../auth/useAuth';
import { AuthLayout } from '../auth/AuthLayout';
import { AuthForm } from '../auth/AuthForm';
import { AuthField } from '../auth/AuthField';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: SubmitEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login(email, password);
      navigate('/', { replace: true });
    } catch (err) {
      setError(loginErrorMessage(err));
      setSubmitting(false);
    }
  }

  return (
    <AuthLayout title="Welcome back" subtitle="Log in to see your weather forecasts">
      <AuthForm
        onSubmit={handleSubmit}
        error={error}
        submitting={submitting}
        submitLabel="Log in"
        submittingLabel="Logging in…"
      >
        <AuthField
          id="login-email"
          label="Email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={setEmail}
        />
        <AuthField
          id="login-password"
          label="Password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={setPassword}
        />
      </AuthForm>

      <p className="text-center mt-3 mb-0">
        Don&apos;t have an account? <Link to="/register">Create one</Link>
      </p>
    </AuthLayout>
  );
}

// A2: invalid credentials. E1: Authentication Service unavailable.
function loginErrorMessage(err: unknown): string {
  if (err instanceof AppwriteException) {
    if (err.code === 401) {
      return 'Incorrect email or password.';
    }
    return 'The authentication service is unavailable right now. Please try again shortly.';
  }
  return 'Something went wrong. Please try again.';
}
