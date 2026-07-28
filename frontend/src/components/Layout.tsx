import type { ReactNode } from 'react'
import NavBar from './NavBar'

interface LayoutProps {
  children: ReactNode
  currentPath: string
  onNavigate: (path: string) => void
}

export default function Layout({ children, currentPath, onNavigate }: LayoutProps) {
  return (
    <div className="app-shell">
      <NavBar currentPath={currentPath} onNavigate={onNavigate} />
      <main className="container">{children}</main>
    </div>
  )
}
