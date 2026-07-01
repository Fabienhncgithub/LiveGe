import { NavLink } from 'react-router-dom'

export default function NavBar() {
  return (
    <header className="navbar">
      <div className="navbar__brand">
        <span className="brand-title">Frontière GE</span>
        <span className="brand-subtitle">Radar Genève</span>
      </div>
      <nav className="navbar__links">
        <NavLink
          to="/dashboard"
          className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
        >
          Dashboard
        </NavLink>
        <NavLink to="/alerts" className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}>
          Alerts
        </NavLink>
        <NavLink to="/settings" className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}>
          Settings
        </NavLink>
      </nav>
    </header>
  )
}
