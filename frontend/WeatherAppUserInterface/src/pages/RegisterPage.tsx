import { useState } from 'react';
import type { SubmitEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AppwriteException } from 'appwrite';
import { useAuth } from '../auth/useAuth';
import { AuthLayout } from '../auth/AuthLayout';
import { AuthForm } from '../auth/AuthForm';
import { AuthField } from '../auth/AuthField';

export function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: SubmitEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await register(userName, email, password);
      navigate('/', { replace: true });
    } catch (err) {
      setError(registerErrorMessage(err));
      setSubmitting(false);
    }
  }

  return (
    <AuthLayout
      title="Create your account"
      subtitle="Track weather forecasts for the cities you care about"
    >
      <AuthForm
        onSubmit={handleSubmit}
        error={error}
        submitting={submitting}
        submitLabel="Create account"
        submittingLabel="Creating account…"
      >
        <AuthField
          id="register-name"
          label="Username"
          type="text"
          autoComplete="userName"
          value={userName}
          onChange={setUserName}
        />
        <AuthField
          id="register-email"
          label="Email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={setEmail}
        />
        <AuthField
          id="register-password"
          label="Password"
          type="password"
          autoComplete="new-password"
          minLength={8}
          value={password}
          onChange={setPassword}
        />
      </AuthForm>

      <p className="text-center mt-3 mb-0">
        Already have an account? <Link to="/login">Log in</Link>
      </p>
    </AuthLayout>
  );
}

// A1 create-account errors: duplicate email, or a rejected password/email from Appwrite.
function registerErrorMessage(err: unknown): string {
  if (err instanceof AppwriteException) {
    if (err.code === 409) {
      return 'An account with that email already exists.';
    }
    if (err.code === 400) {
      return err.message;
    }
    return 'The authentication service is unavailable right now. Please try again shortly.';
  }
  return 'Something went wrong. Please try again.';
}
