type JsonValue = object | Array<unknown>

const baseUrl =
  (import.meta.env.VITE_API_BASE_URL as string | undefined)
  ?? (import.meta.env.DEV ? 'http://127.0.0.1:5090' : '')
const adminKeyStorageKey = 'frontiere-live-ge.admin-key'

const getAdminKey = () => sessionStorage.getItem(adminKeyStorageKey)

async function request<T>(path: string, options: RequestInit, admin = false): Promise<T> {
  const adminKey = admin ? getAdminKey() : null
  const response = await fetch(`${baseUrl}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(adminKey ? { 'X-Admin-Key': adminKey } : {}),
      ...(options.headers ?? {})
    }
  })

  if (!response.ok) {
    if (admin && response.status === 401) {
      sessionStorage.removeItem(adminKeyStorageKey)
    }
    const message = await response.text()
    throw new Error(message || `HTTP ${response.status}`)
  }

  return response.json() as Promise<T>
}

export function getJson<T>(path: string): Promise<T> {
  return request<T>(path, { method: 'GET' })
}

export function putJson<T>(path: string, body: JsonValue): Promise<T> {
  return request<T>(path, { method: 'PUT', body: JSON.stringify(body) })
}

export function postJson<T>(path: string, body?: JsonValue): Promise<T> {
  return request<T>(path, {
    method: 'POST',
    body: body ? JSON.stringify(body) : undefined
  })
}

export function getAdminJson<T>(path: string): Promise<T> {
  return request<T>(path, { method: 'GET' }, true)
}

export function putAdminJson<T>(path: string, body: JsonValue): Promise<T> {
  return request<T>(path, { method: 'PUT', body: JSON.stringify(body) }, true)
}

export function postAdminJson<T>(path: string, body?: JsonValue): Promise<T> {
  return request<T>(
    path,
    {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined
    },
    true
  )
}

export function setAdminKey(value: string) {
  sessionStorage.setItem(adminKeyStorageKey, value)
}

export function clearAdminKey() {
  sessionStorage.removeItem(adminKeyStorageKey)
}

export function hasAdminKey() {
  return Boolean(getAdminKey())
}

export { baseUrl }
