import { defineStore } from 'pinia'
import { api } from '../services/api'
import { useAuthStore } from './auth'

export const useContatosStore = defineStore('contatos', {
  state: () => ({ contatos: [], comHistorico: [], carregando: false, erro: '' }),
  actions: {
    async carregar() {
      const auth = useAuthStore()
      this.carregando = true
      this.erro = ''
      try {
        const [lista, conversas] = await Promise.all([
          api('/api/contatos', { token: auth.token }),
          api('/api/conversas', { token: auth.token }).catch(() => [])
        ])
        this.contatos = lista.filter((c) => c.id !== auth.usuario?.id)
        this.comHistorico = (Array.isArray(conversas) ? conversas : []).map((c) => c.id)
      } catch (e) {
        this.erro = e.message || 'Não foi possível carregar os contatos.'
      } finally {
        this.carregando = false
      }
    }
  }
})
