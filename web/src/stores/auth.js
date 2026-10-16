import { defineStore } from 'pinia'
import { api } from '../services/api'

const CHAVE = 'intranet.sessao'

function carregar() {
  try {
    const s = JSON.parse(localStorage.getItem(CHAVE))
    if (s && new Date(s.expiraEm) > new Date()) return s
    if (s) localStorage.setItem('intranet.expirou', '1')
  } catch { /* ignora */ }
  localStorage.removeItem(CHAVE)
  return null
}

export const useAuthStore = defineStore('auth', {
  state: () => {
    const s = carregar()
    return { token: s?.token || null, expiraEm: s?.expiraEm || null, usuario: s?.usuario || null }
  },
  getters: {
    logado: (st) => !!st.token && new Date(st.expiraEm) > new Date()
  },
  actions: {
    async entrar(email, senha) {
      const r = await api('/api/auth/login', { method: 'POST', corpo: { email, senha } })
      this.token = r.token
      this.expiraEm = r.expiraEm
      this.usuario = r.usuario
      localStorage.setItem(CHAVE, JSON.stringify(r))
      localStorage.removeItem('intranet.expirou')
    },
    sair() {
      this.token = null
      this.expiraEm = null
      this.usuario = null
      localStorage.removeItem(CHAVE)
    }
  }
})
