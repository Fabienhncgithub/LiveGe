interface StatusBadgeProps {
  label: string
  variant:
    | 'green'
    | 'orange'
    | 'red'
    | 'info'
    | 'warning'
    | 'critical'
    | 'rising'
    | 'stable'
    | 'falling'
}

export default function StatusBadge({ label, variant }: StatusBadgeProps) {
  return <span className={`badge badge--${variant}`}>{label}</span>
}
