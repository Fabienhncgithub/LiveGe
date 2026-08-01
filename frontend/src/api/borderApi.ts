import { getAdminJson, getJson, postAdminJson, putAdminJson } from './http'
import type {
  AlertEvent,
  BorderPoint,
  BotSettings,
  DirectionalTraffic,
  TrafficHistoryEntry,
  TrafficQuotaStatus,
  LiveBorderStatus,
  MobilityAdvice,
  RunSummary,
  TrafficSnapshot,
  TrafficForecast
} from '../types'

export const fetchBorderPoints = () => getJson<BorderPoint[]>('/api/border-points')

export const fetchLiveStatuses = () => getJson<LiveBorderStatus[]>('/api/live')

export const fetchDirectionalTraffic = () => getJson<DirectionalTraffic[]>('/api/live/directions')

export const fetchMobilityAdvice = () => getJson<MobilityAdvice>('/api/live/advice')

export const fetchTrafficQuota = () => getJson<TrafficQuotaStatus>('/api/traffic/quota')

export const fetchTrafficHistory = () => getJson<TrafficHistoryEntry[]>('/api/traffic/history')

export const fetchTrafficForecast = () => getJson<TrafficForecast>('/api/traffic/forecast')

export const fetchAlerts = () => getJson<AlertEvent[]>('/api/alerts')

export const fetchHistory = (borderPointId: number) =>
  getJson<TrafficSnapshot[]>(`/api/history/${borderPointId}`)

export const fetchSettings = () => getAdminJson<BotSettings>('/api/admin/settings')

export const updateSettings = (payload: BotSettings) =>
  putAdminJson<BotSettings>('/api/admin/settings', payload)

export const runOnce = () => postAdminJson<RunSummary>('/api/admin/run-once', {})
