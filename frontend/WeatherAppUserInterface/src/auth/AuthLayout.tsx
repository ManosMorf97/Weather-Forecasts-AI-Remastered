import type { ReactNode } from 'react';

interface AuthLayoutProps {
  title: string;
  subtitle: string;
  children: ReactNode;
}

// Shared card shell for Login/Register so both stay visually consistent.
export function AuthLayout({ title, subtitle, children }: AuthLayoutProps) {
  return (
    <div className="d-flex justify-content-center align-items-center flex-grow-1 px-3 py-5">
      <div className="card border-0" style={{ width: '100%', maxWidth: 400 }}>
        <div className="card-body p-4 p-sm-5">
          <div className="text-center mb-4">
            <WeatherBrandIcon />
            <h1 className="h3 mb-1 mt-3">{title}</h1>
            <p className="text-muted mb-0">{subtitle}</p>
          </div>
          {children}
        </div>
      </div>
    </div>
  );
}

function WeatherBrandIcon() {
  return (
    <svg width="48" height="48" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="16" cy="8" r="4.5" fill="#fbbf24" />
      <path
        d="M7 19a4 4 0 0 1-.6-7.96A5 5 0 0 1 16 9.5a3.5 3.5 0 0 1-.5 6.98"
        fill="var(--accent)"
        stroke="var(--accent)"
        strokeWidth="1"
      />
    </svg>
  );
}
