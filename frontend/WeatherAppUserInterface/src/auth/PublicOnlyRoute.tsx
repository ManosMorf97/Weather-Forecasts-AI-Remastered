import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './useAuth';
import { destinationFor } from './destination';
import { LoadingScreen } from './LoadingScreen';

// Login/register are the only pages usable without a session; an already-authenticated
// user landing here is sent straight to their post-login destination instead.
export function PublicOnlyRoute() {
  const { status, hasCitySiteSelection } = useAuth();

  if (status === 'loading') {
    return <LoadingScreen />;
  }
  if (status === 'authenticated') {
    return <Navigate to={destinationFor(hasCitySiteSelection)} replace />;
  }
  return <Outlet />;
}
