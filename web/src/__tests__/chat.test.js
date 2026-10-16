import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ConversaJanela from '../views/ConversaJanela.vue'
import { useChatStore, dentroDoPrazo } from '../stores/chat'
import { useAuthStore } from '../stores/auth'

const agoraIso = (minAtras) => new Date(Date.now() - minAtras * 60000).toISOString()
const msg = (id, rem, min, extra = {}) => ({ id, remetenteId: rem, destinatarioId: rem === 1 ? 2 : 1, texto: `m${id}`, enviadaEm: agoraIso(min), editada: false, apagada: false, ...extra })

async function montar(mensagens, somenteLeitura = false) {
  globalThis.fetch = vi.fn(() => Promise.resolve({ ok: true, status: 200, headers: { get: () => 'false' }, json: () => Promise.resolve(mensagens) }))
  const pinia = createPinia()
  setActivePinia(pinia)
  useAuthStore().usuario = { id: 1 }
  const chat = useChatStore()
  await chat.abrir({ id: 2, nome: 'Ana' }, somenteLeitura)
  const w = mount(ConversaJanela, { global: { plugins: [pinia] } })
  await flushPromises()
  return { w, chat }
}

describe('conversa', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('prazo de 15 minutos', () => {
    expect(dentroDoPrazo(msg(1, 1, 14))).toBe(true)
    expect(dentroDoPrazo(msg(1, 1, 16))).toBe(false)
  })

  it('editar/apagar só nas minhas mensagens dentro do prazo', async () => {
    const { w } = await montar([msg(1, 1, 5), msg(2, 2, 5), msg(3, 1, 20)])
    expect(w.findAll('.acoes').length).toBe(1)
  })

  it('mensagem apagada mostra o aviso e sem ações', async () => {
    const { w } = await montar([msg(1, 1, 1, { apagada: true, texto: 'x' })])
    expect(w.text()).toContain('mensagem apagada')
    expect(w.find('.acoes').exists()).toBe(false)
  })

  it('mensagem em tempo real aparece e duplicata é ignorada', async () => {
    const { w, chat } = await montar([msg(1, 2, 1)])
    chat.receber(msg(2, 2, 0))
    chat.receber(msg(2, 2, 0))
    chat.receber({ ...msg(9, 3, 0), destinatarioId: 4 }) // de terceiros
    await flushPromises()
    expect(w.findAll('.balao').length).toBe(2)
  })

  it('apagada via evento troca o texto', async () => {
    const { w, chat } = await montar([msg(1, 2, 1)])
    chat.atualizar({ id: 1, apagada: true })
    await flushPromises()
    expect(w.text()).toContain('mensagem apagada')
  })

  it('somente leitura não mostra campo de envio', async () => {
    const { w } = await montar([msg(1, 2, 1)], true)
    expect(w.find('form').exists()).toBe(false)
  })
})
