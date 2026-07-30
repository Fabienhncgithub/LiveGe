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

export interface DirectionalTraffic {
  borderPointName: string
  direction: 'ToGeneva' | 'ToFrance'
  directionLabel: string
  isAvailable: boolean
  travelTimeMinutes?: number | null
  freeFlowTimeMinutes?: number | null
  delayMinutes?: number | null
  congestionLevel: CongestionLevel | 'Unknown'
  trend: TrendDirection | 'Unknown'
  sourceName: string
  observedAtUtc?: string | null
  confidencePercent: number
  unavailableReason?: string | null
}

export interface HereQuotaStatus {
  dateUtc: string
  requestsUsed: number
  dailyLimit: number
  requestsRemaining: number
  usagePercent: number
  level: 'Normal' | 'Warning' | 'Critical'
  message: string
  resetsAtUtc: string
}

export interface HereHistoryEntry {
  id: number
  borderPointName: string
  direction: 'ToGeneva' | 'ToFrance'
  directionLabel: string
  observedAtUtc: string
  delayMinutes: number
  congestionLevel: CongestionLevel
}

export interface TrafficForecastSuggestion {
  direction: 'ToGeneva' | 'ToFrance'
  directionLabel: string
  bestDay: string
  bestHourStart: number
  averageDelayMinutes: number
  sampleSize: number
  confidencePercent: number
  advice: string
}

export interface TrafficForecast {
  isAvailable: boolean
  samplesCount: number
  daysCovered: number
  minimumDaysRequired: number
  message: string
  suggestions: TrafficForecastSuggestion[]
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
