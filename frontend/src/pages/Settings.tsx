import { useEffect, useState } from 'react'
import { fetchSettings, runOnce, updateSettings } from '../api/borderApi'
import SettingsForm from '../components/SettingsForm'
import type { BotSettings } from '../types'

export default function Settings() {
  const [settings, setSettings] = useState<BotSettings | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [running, setRunning] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const load = async () => {
      try {
        setError(null)
        const data = await fetchSettings()
        setSettings(data)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Erreur inconnue')
      } finally {
        setLoading(false)
      }
    }

    void load()
  }, [])

  const handleSave = async (payload: BotSettings) => {
    try {
      setSaving(true)
      setMessage(null)
      setError(null)
      const updated = await updateSettings(payload)
      setSettings(updated)
      setMessage('Paramètres mis à jour.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur lors de la mise à jour')
    } finally {
      setSaving(false)
    }
  }

  const handleRunNow = async () => {
    try {
      setRunning(true)
      setMessage(null)
      setError(null)
      const result = await runOnce()
      setMessage(`Cycle exécuté. Snapshots: ${result.snapshotsCreated}, alertes: ${result.alertsCreated}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur lors de l\u0027exécution')
    } finally {
      setRunning(false)
    }
  }

  return (
    <section className="page">
      <div className="page-header">
        <div>
          <h1>Réglages</h1>
          <p className="subtitle">Bot & publication</p>
        </div>
      </div>

      {loading ? (
        <p className="empty-state">Chargement...</p>
      ) : (
        <>
          {message && <div className="success-banner">{message}</div>}
          {error && <div className="error-banner">{error}</div>}
          <SettingsForm
            settings={settings}
            onSave={handleSave}
            onRunNow={handleRunNow}
            saving={saving}
            running={running}
          />
        </>
      )}
    </section>
  )
}
