import { getAdminJson, getJson, postAdminJson, putAdminJson } from './http'
import type {
  AlertEvent,
  BorderPoint,
  BotSettings,
  DirectionalTraffic,
  HereHistoryEntry,
  HereQuotaStatus,
  LiveBorderStatus,
  RunSummary,
  TrafficSnapshot,
  TrafficForecast
} from '../types'

export const fetchBorderPoints = () => getJson<BorderPoint[]>('/api/border-points')

export const fetchLiveStatuses = () => getJson<LiveBorderStatus[]>('/api/live')

export const fetchDirectionalTraffic = () => getJson<DirectionalTraffic[]>('/api/live/directions')

export const fetchHereQuota = () => getJson<HereQuotaStatus>('/api/here/quota')

export const fetchHereHistory = () => getJson<HereHistoryEntry[]>('/api/here/history')

export const fetchTrafficForecast = () => getJson<TrafficForecast>('/api/here/forecast')

export const fetchAlerts = () => getJson<AlertEvent[]>('/api/alerts')

export const fetchHistory = (borderPointId: number) =>
  getJson<TrafficSnapshot[]>(`/api/history/${borderPointId}`)

export const fetchSettings = () => getAdminJson<BotSettings>('/api/admin/settings')

export const updateSettings = (payload: BotSettings) =>
  putAdminJson<BotSettings>('/api/admin/settings', payload)

export const runOnce = () => postAdminJson<RunSummary>('/api/admin/run-once', {})
