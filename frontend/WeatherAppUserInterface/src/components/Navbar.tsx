import { NavLink } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { LogoutBar } from '../auth/LogoutBar';

const NAV_LINKS = [
  { to: '/dashboard', label: 'Current predictions' },
  { to: '/aggregated', label: 'Suggested Forecasts' },
  { to: '/setup', label: 'Set Selections' },
];

// Top bar for authenticated pages: page links on the left, "Log out" on the right. The links
// only show once the user has a CitySite selection - before that, /setup is the only place to be.
export function Navbar() {
  const { hasCitySiteSelection } = useAuth();

  if (!hasCitySiteSelection) {
    return <LogoutBar />;
  }

  return (
    <header className="d-flex flex-wrap align-items-center justify-content-between">
      <nav aria-label="Main" className="px-3 pt-3">
        <ul className="nav nav-pills app-nav gap-1">
          {NAV_LINKS.map(({ to, label }) => (
            <li className="nav-item" key={to}>
              <NavLink to={to} className="nav-link">
                {label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
      <LogoutBar />
    </header>
  );
}
