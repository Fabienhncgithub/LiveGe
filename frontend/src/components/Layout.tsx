import type { ReactNode } from 'react'
import NavBar from './NavBar'

interface LayoutProps {
  children: ReactNode
}

export default function Layout({ children }: LayoutProps) {
  return (
    <div className="app-shell">
      <NavBar />
      <main className="container">{children}</main>
    </div>
  )
}
