import { useAuth } from '../auth/useAuth';

// Placeholder landing spot for a user with no CitySite selection yet (UC2 step 6).
// Select Cities (UC4) and Select Forecasting Services (UC5) will replace this.
export function SetupPage() {
  const { logout } = useAuth();

  return (
    <div className="d-flex justify-content-center align-items-center vh-100 px-3">
      <div className="card shadow-sm" style={{ width: '100%', maxWidth: 380 }}>
        <div className="card-body p-4 text-center">
          <h1 className="h3 mb-1">Let&apos;s set things up</h1>
          <p className="text-muted mb-4">
            City and forecasting service selection isn&apos;t built yet.
          </p>
          <button className="btn btn-primary w-100" onClick={() => void logout()}>
            Log out
          </button>
        </div>
      </div>
    </div>
  );
}
