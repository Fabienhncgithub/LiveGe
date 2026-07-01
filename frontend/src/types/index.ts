export type CongestionLevel = 'Green' | 'Orange' | 'Red'
export type AlertSeverity = 'Info' | 'Warning' | 'Critical'
export type TrendDirection = 'Rising' | 'Stable' | 'Falling'

export interface BorderPoint {
  id: number
  name: string
  latitude: number
  longitude: number
  isActive: boolean
}

export interface LiveBorderStatus {
  borderPointId: number
  borderPointName: string
  estimatedDelayMinutes: number
  speedKmh: number
  congestionLevel: CongestionLevel
  trend: TrendDirection
  predictedDelayMinutes?: number | null
  predictionLabel: string
  recordedAtUtc: string
}

export interface AlertEvent {
  id: number
  borderPointId: number
  borderPointName: string
  createdAtUtc: string
  message: string
  severity: AlertSeverity
  trend: TrendDirection
  isPosted: boolean
  postedAtUtc?: string | null
  predictedDelayMinutes?: number | null
}

export interface TrafficSnapshot {
  id: number
  borderPointId: number
  recordedAtUtc: string
  estimatedDelayMinutes: number
  speedKmh: number
  congestionLevel: CongestionLevel
  sourceName: string
}

export interface BotSettings {
  postingEnabled: boolean
  minMinutesBetweenPosts: number
  risingThresholdMinutes: number
  criticalDelayMinutes: number
}

export interface RunSummary {
  snapshotsCreated: number
  alertsCreated: number
  alertsPosted: number
  ranAtUtc: string
}
