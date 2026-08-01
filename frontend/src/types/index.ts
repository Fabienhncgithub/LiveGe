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
  isStale: boolean
  ageMinutes?: number | null
  unavailableReason?: string | null
}

export type AdviceRecommendation =
  | 'Recommended'
  | 'Equivalent'
  | 'Alternative'
  | 'Avoid'
  | 'Unavailable'

export type SourceAvailability = 'Online' | 'Stale' | 'Unavailable'

export interface AdviceReason {
  kind: string
  label: string
  sourceName: string
  severity: string
}

export interface RouteAdvice {
  borderPointName: string
  direction: 'ToGeneva' | 'ToFrance'
  directionLabel: string
  isAvailable: boolean
  travelTimeMinutes?: number | null
  freeFlowTimeMinutes?: number | null
  delayMinutes?: number | null
  congestionLevel: CongestionLevel | 'Unknown'
  trend: TrendDirection | 'Unknown'
  observedAtUtc?: string | null
  isStale: boolean
  ageMinutes?: number | null
  dataCoveragePercent: number
  contextRiskPoints: number
  decisionCost: number
  recommendation: AdviceRecommendation
  delayAdvantageMinutes?: number | null
  reasons: AdviceReason[]
  nearbySignalIds: string[]
  unavailableReason?: string | null
}

export interface DataSourceStatus {
  id: string
  name: string
  status: SourceAvailability
  isOfficial: boolean
  hasBillingRisk: boolean
  recordsCount: number
  relevantSignalsCount: number
  checkedAtUtc: string
  dataTimestampUtc?: string | null
  coverage: string
  attribution: string
  sourceUrl: string
  message?: string | null
}

export interface RoadSignal {
  id: string
  sourceId: string
  sourceName: string
  category: string
  severity: string
  title: string
  description: string
  latitude?: number | null
  longitude?: number | null
  travelDirectionDegrees?: number | null
  appliesToAllRoutes: boolean
  startsAtUtc?: string | null
  endsAtUtc?: string | null
  observedAtUtc?: string | null
  detailsUrl?: string | null
}

export interface MobilityAdvice {
  generatedAtUtc: string
  algorithmVersion: string
  scopeNotice: string
  routes: RouteAdvice[]
  sources: DataSourceStatus[]
  signals: RoadSignal[]
}

export interface TrafficQuotaStatus {
  monthUtc: string
  requestsUsed: number
  monthlyLimit: number
  requestsRemaining: number
  usagePercent: number
  level: 'Normal' | 'Warning' | 'Critical'
  message: string
  resetsAtUtc: string
}

export interface TrafficHistoryEntry {
  id: number
  borderPointName: string
  direction: 'ToGeneva' | 'ToFrance'
  directionLabel: string
  observedAtUtc: string
  delayMinutes: number
  congestionLevel: CongestionLevel
}

export interface TrafficForecastSuggestion {
  borderPointId: number
  borderPointName: string
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
