interface NavBarProps {
  currentPath: string
  onNavigate: (path: string) => void
}

const links = [
  { path: '/dashboard', label: 'Dashboard' },
  { path: '/alerts', label: 'Alertes' },
  { path: '/settings', label: 'Administration' }
]

export default function NavBar({ currentPath, onNavigate }: NavBarProps) {
  return (
    <header className="navbar">
      <div className="navbar__brand">
        <span className="brand-title">Frontière GE</span>
        <span className="brand-subtitle">Radar Genève</span>
      </div>
      <nav className="navbar__links">
        {links.map((link) => (
          <a
            key={link.path}
            href={link.path}
            className={currentPath === link.path ? 'nav-link active' : 'nav-link'}
            aria-current={currentPath === link.path ? 'page' : undefined}
            onClick={(event) => {
              event.preventDefault()
              onNavigate(link.path)
            }}
          >
            {link.label}
          </a>
        ))}
      </nav>
    </header>
  )
}
