import { useAuth } from '../auth/useAuth';

// Placeholder landing spot for an already-configured user (UC2 step 7).
export function DashboardPage() {
  const { logout } = useAuth();

  return (
    <div className="d-flex justify-content-center align-items-center vh-100 px-3">
      <div className="card shadow-sm" style={{ width: '100%', maxWidth: 380 }}>
        <div className="card-body p-4 text-center">
          <h1 className="h3 mb-1">Dashboard</h1>
          <p className="text-muted mb-4">Coming soon.</p>
          <button className="btn btn-primary w-100" onClick={() => void logout()}>
            Log out
          </button>
        </div>
      </div>
    </div>
  );
}
