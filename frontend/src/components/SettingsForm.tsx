import { useEffect, useState } from 'react'
import type { BotSettings } from '../types'

interface SettingsFormProps {
  settings: BotSettings | null
  onSave: (payload: BotSettings) => Promise<void>
  onRunNow: () => Promise<void>
  saving?: boolean
  running?: boolean
}

export default function SettingsForm({
  settings,
  onSave,
  onRunNow,
  saving = false,
  running = false
}: SettingsFormProps) {
  const [form, setForm] = useState<BotSettings | null>(settings)

  useEffect(() => {
    setForm(settings)
  }, [settings])

  if (!form) {
    return <p className="empty-state">Chargement...</p>
  }

  const updateField = (key: keyof BotSettings, value: boolean | number) => {
    setForm((prev) => (prev ? { ...prev, [key]: value } : prev))
  }

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault()
    await onSave(form)
  }

  return (
    <form className="settings-form" onSubmit={handleSubmit}>
      <label className="form-row">
        <span>Publication</span>
        <input
          type="checkbox"
          checked={form.postingEnabled}
          onChange={(event) => updateField('postingEnabled', event.target.checked)}
        />
      </label>

      <label className="form-row">
        <span>Intervalle (min)</span>
        <input
          type="number"
          min={30}
          value={form.minMinutesBetweenPosts}
          onChange={(event) => updateField('minMinutesBetweenPosts', Number(event.target.value))}
        />
      </label>

      <label className="form-row">
        <span>Seuil hausse</span>
        <input
          type="number"
          min={5}
          value={form.risingThresholdMinutes}
          onChange={(event) => updateField('risingThresholdMinutes', Number(event.target.value))}
        />
      </label>

      <label className="form-row">
        <span>Seuil critique</span>
        <input
          type="number"
          min={20}
          value={form.criticalDelayMinutes}
          onChange={(event) => updateField('criticalDelayMinutes', Number(event.target.value))}
        />
      </label>

      <p className="settings-hint">
        Mode sobre: les posts X partent seulement pour les alertes utiles, avec un delai minimum entre deux
        publications.
      </p>

      <div className="form-actions">
        <button type="submit" className="btn" disabled={saving}>
          {saving ? 'Sauve...' : 'Sauver'}
        </button>
        <button type="button" className="btn btn-secondary" onClick={onRunNow} disabled={running}>
          {running ? 'Lance...' : 'Lancer'}
        </button>
      </div>
    </form>
  )
}
