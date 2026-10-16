import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ContatosLista from '../views/ContatosLista.vue'
import { useAuthStore } from '../stores/auth'

const contatos = [
  { id: 1, nome: 'Eu Mesma', setor: 'TI', ativo: true },
  { id: 2, nome: 'Ana Souza', setor: 'Edição', ativo: true },
  { id: 3, nome: 'Carlos Lima', setor: 'Arte', ativo: false },
  { id: 4, nome: 'Dani Reis', setor: 'RH', ativo: false }
]

function mockFetch(conversas) {
  globalThis.fetch = vi.fn((url) => Promise.resolve({
    ok: true,
    json: () => Promise.resolve(url.endsWith('/api/contatos') ? contatos : conversas)
  }))
}

async function montar(conversas) {
  mockFetch(conversas)
  const pinia = createPinia()
  setActivePinia(pinia)
  useAuthStore().usuario = { id: 1 }
  const w = mount(ContatosLista, { global: { plugins: [pinia] } })
  await flushPromises()
  return w
}

beforeEach(() => localStorage.clear())

describe('ContatosLista', () => {
  it('mostra ativos e inativos (sem o próprio usuário) com etiqueta', async () => {
    const w = await montar([])
    expect(w.findAll('.contato')).toHaveLength(3)
    expect(w.findAll('.etiqueta')).toHaveLength(2)
    expect(w.text()).not.toContain('Eu Mesma')
  })
  it('inativo sem histórico não é selecionável', async () => {
    const w = await montar([{ id: 3 }])
    const botoes = w.findAll('.contato')
    expect(botoes[1].attributes('disabled')).toBeUndefined()
    expect(botoes[2].attributes('disabled')).toBeDefined()
    await botoes[2].trigger('click')
    expect(w.emitted('abrir')).toBeUndefined()
  })
  it('inativo com histórico abre somente leitura; ativo abre normal', async () => {
    const w = await montar([{ id: 3 }])
    const botoes = w.findAll('.contato')
    await botoes[1].trigger('click')
    await botoes[0].trigger('click')
    expect(w.emitted('abrir')[0][0].somenteLeitura).toBe(true)
    expect(w.emitted('abrir')[1][0].somenteLeitura).toBe(false)
  })
})
