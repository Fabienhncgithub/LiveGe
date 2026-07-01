type JsonValue = object | Array<unknown>

const baseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? 'http://localhost:5000'

async function request<T>(path: string, options: RequestInit): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers ?? {})
    }
  })

  if (!response.ok) {
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

export { baseUrl }
