import type { ReactNode, SubmitEvent } from 'react';

interface AuthFormProps {
  onSubmit: (event: SubmitEvent) => void;
  error: string | null;
  submitting: boolean;
  submitLabel: string;
  submittingLabel: string;
  children: ReactNode;
}

// Shared <form> shell for Login/Register: error alert + fields (as children) + submit button.
export function AuthForm({
  onSubmit,
  error,
  submitting,
  submitLabel,
  submittingLabel,
  children,
}: AuthFormProps) {
  return (
    <form onSubmit={onSubmit}>
      {error && <div className="alert alert-danger py-2">{error}</div>}

      {children}

      <button className="btn btn-primary w-100" type="submit" disabled={submitting}>
        {submitting ? submittingLabel : submitLabel}
      </button>
    </form>
  );
}
