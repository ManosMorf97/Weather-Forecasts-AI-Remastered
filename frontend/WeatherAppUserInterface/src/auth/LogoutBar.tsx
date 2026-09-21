import { useAuth } from './useAuth';

// Top-right "Log out" control shared by authenticated pages.
export function LogoutBar() {
  const { logout } = useAuth();

  return (
    <div className="d-flex justify-content-end px-3 pt-3 ms-auto">
      <button className="btn btn-outline-secondary" type="button" onClick={() => void logout()}>
        Log out
      </button>
    </div>
  );
}
