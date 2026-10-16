import { defineStore } from 'pinia'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { api } from '../services/api'
import { useAuthStore } from './auth'

const BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000'
export const JANELA_MS = 15 * 60 * 1000

export function dentroDoPrazo(msg, agora = Date.now()) {
  return agora - new Date(msg.enviadaEm).getTime() <= JANELA_MS
}

export const useChatStore = defineStore('chat', {
  state: () => ({
    contato: null,
    somenteLeitura: false,
    mensagens: [],
    temMais: false,
    carregando: false,
    erro: '',
    conectado: false
  }),
  actions: {
    async abrir(contato, somenteLeitura = false) {
      const auth = useAuthStore()
      this.contato = contato
      this.somenteLeitura = somenteLeitura
      this.mensagens = []
      this.erro = ''
      this.temMais = false
      this.carregando = true
      const alvo = contato.id
      try {
        let tem = false
        const lista = await api(`/api/conversas/${alvo}/mensagens`, {
          token: auth.token,
          aoResponder: (r) => { tem = r.headers.get('X-Tem-Mais') === 'true' }
        })
        if (this.contato?.id !== alvo) return
        this.mensagens = lista
        this.temMais = tem
      } catch (e) {
        this.erro = e.message
      } finally {
        this.carregando = false
      }
    },
    async carregarAntigas() {
      if (!this.temMais || this.carregando || !this.mensagens.length) return
      const auth = useAuthStore()
      const alvo = this.contato.id
      this.carregando = true
      try {
        let tem = false
        const lista = await api(`/api/conversas/${alvo}/mensagens?antes=${this.mensagens[0].id}`, {
          token: auth.token,
          aoResponder: (r) => { tem = r.headers.get('X-Tem-Mais') === 'true' }
        })
        if (this.contato?.id !== alvo) return
        this.mensagens = [...lista, ...this.mensagens]
        this.temMais = tem
      } catch (e) {
        this.erro = e.message
      } finally {
        this.carregando = false
      }
    },
    async enviar(texto) {
      const auth = useAuthStore()
      this.erro = ''
      try {
        const m = await api(`/api/conversas/${this.contato.id}/mensagens`, { method: 'POST', corpo: { texto }, token: auth.token })
        this.receber(m) // o evento do hub também chega; receber() ignora duplicata
      } catch (e) {
        this.erro = e.message
        throw e
      }
    },
    async editar(id, texto) {
      const auth = useAuthStore()
      this.erro = ''
      try {
        this.atualizar(await api(`/api/mensagens/${id}`, { method: 'PUT', corpo: { texto }, token: auth.token }))
      } catch (e) {
        this.erro = e.message
        throw e
      }
    },
    async apagar(id) {
      const auth = useAuthStore()
      this.erro = ''
      try {
        const r = await api(`/api/mensagens/${id}`, { method: 'DELETE', token: auth.token })
        if (r && r.id) this.atualizar(r)
        else this.atualizar({ id, apagada: true })
      } catch (e) {
        this.erro = e.message
        throw e
      }
    },
    pertenceAConversa(m) {
      const eu = useAuthStore().usuario?.id
      const outro = this.contato?.id
      return !!outro && ((m.remetenteId === eu && m.destinatarioId === outro) || (m.remetenteId === outro && m.destinatarioId === eu))
    },
    receber(m) {
      if (!this.pertenceAConversa(m)) return
      if (this.mensagens.some((x) => x.id === m.id)) return
      this.mensagens.push(m)
    },
    atualizar(m) {
      const i = this.mensagens.findIndex((x) => x.id === m.id)
      if (i < 0) return
      const nova = { ...this.mensagens[i], ...m }
      if (nova.apagada) nova.texto = 'mensagem apagada'
      this.mensagens[i] = nova
    },
    conectar() {
      if (this._hub) return
      const auth = useAuthStore()
      const hub = new HubConnectionBuilder()
        .withUrl(`${BASE}/hubs/chat`, { accessTokenFactory: () => auth.token })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build()
      hub.on('mensagemRecebida', (m) => this.receber(m))
      hub.on('mensagemEditada', (m) => this.atualizar(m))
      hub.on('mensagemApagada', (m) => this.atualizar({ ...m, apagada: true }))
      hub.onreconnecting(() => { this.conectado = false })
      hub.onreconnected(() => {
        this.conectado = true
        if (this.contato) this.abrir(this.contato, this.somenteLeitura) // recupera o que perdeu
      })
      hub.onclose(() => { this.conectado = false })
      this._hub = hub
      hub.start().then(() => { this.conectado = true }).catch(() => { this.conectado = false })
    },
    desconectar() {
      this._hub?.stop()
      this._hub = null
      this.conectado = false
      this.contato = null
      this.mensagens = []
    }
  }
})
