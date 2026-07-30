interface NavBarProps {
  currentPath: string
  onNavigate: (path: string) => void
}

const links = [
  { path: '/dashboard', label: 'Trafic' },
  { path: '/alerts', label: 'Alertes' }
]

export default function NavBar({ currentPath, onNavigate }: NavBarProps) {
  return (
    <header className="navbar">
      <div className="navbar__brand">
        <span className="brand-mark" aria-hidden="true">
          <svg viewBox="0 0 32 32"><path d="M5 21.5 9.7 10h12.6L27 21.5M10 21.5h12M12.5 15.5h7" /><circle cx="16" cy="9" r="3" /></svg>
        </span>
        <span>
          <span className="brand-title">Frontière<span>GE</span></span>
          <span className="brand-subtitle">Le radar des passages genevois</span>
        </span>
      </div>
      <nav className="navbar__links">
        {links.map((link) => (
          <a key={link.path} href={link.path} className={currentPath === link.path ? 'nav-link active' : 'nav-link'}
            aria-current={currentPath === link.path ? 'page' : undefined}
            onClick={(event) => { event.preventDefault(); onNavigate(link.path) }}>
            {link.label}
          </a>
        ))}
      </nav>
    </header>
  )
}
