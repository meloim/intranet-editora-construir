const BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000'

export class ApiError extends Error {
  constructor(status, message) {
    super(message)
    this.status = status
  }
}

export async function api(caminho, { method = 'GET', corpo, token } = {}) {
  const headers = {}
  if (corpo) headers['Content-Type'] = 'application/json'
  if (token) headers.Authorization = `Bearer ${token}`
  let resp
  try {
    resp = await fetch(BASE + caminho, { method, headers, body: corpo ? JSON.stringify(corpo) : undefined })
  } catch {
    throw new ApiError(0, 'Não foi possível conectar ao servidor. Tente novamente.')
  }
  const dados = await resp.json().catch(() => ({}))
  if (!resp.ok) throw new ApiError(resp.status, dados.erro || 'Algo deu errado.')
  return dados
}
