import { getJson, postJson, putJson } from './http'
import type {
  AlertEvent,
  BorderPoint,
  BotSettings,
  LiveBorderStatus,
  RunSummary,
  TrafficSnapshot
} from '../types'

export const fetchBorderPoints = () => getJson<BorderPoint[]>('/api/border-points')

export const fetchLiveStatuses = () => getJson<LiveBorderStatus[]>('/api/live')

export const fetchAlerts = () => getJson<AlertEvent[]>('/api/alerts')

export const fetchHistory = (borderPointId: number) =>
  getJson<TrafficSnapshot[]>(`/api/history/${borderPointId}`)

export const fetchSettings = () => getJson<BotSettings>('/api/settings')

export const updateSettings = (payload: BotSettings) =>
  putJson<BotSettings>('/api/settings', payload)

export const runOnce = () => postJson<RunSummary>('/api/run-once', {})
