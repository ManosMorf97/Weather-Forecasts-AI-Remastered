import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './useAuth';
import { LoadingScreen } from './LoadingScreen';

// UC1 A4: route guard - any page other than the login/register form requires a valid
// session; a missing or expired one redirects back to the login form.
export function ProtectedRoute() {
  const { status } = useAuth();

  if (status === 'loading') {
    return <LoadingScreen />;
  }
  if (status === 'unauthenticated') {
    return <Navigate to="/login" replace />;
  }
  return <Outlet />;
}
