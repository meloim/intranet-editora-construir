import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import LoginView from '../views/LoginView.vue'
import { useAuthStore } from '../stores/auth'

function montar(query = {}) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/login', name: 'login', component: LoginView },
      { path: '/', name: 'conversas', component: { template: '<div>conv</div>' } }
    ]
  })
  router.push({ name: 'login', query })
  return router.isReady().then(() => ({ router, w: mount(LoginView, { global: { plugins: [router] } }) }))
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  vi.restoreAllMocks()
})

describe('LoginView', () => {
  it('mostra erro quando campos vazios', async () => {
    const { w } = await montar()
    await w.find('form').trigger('submit')
    expect(w.find('[role=alert]').text()).toContain('Preencha')
  })

  it('mostra aviso de sessão expirada', async () => {
    const { w } = await montar({ expirada: '1' })
    expect(w.text()).toContain('sessão expirou')
  })

  it('401 mostra mensagem clara', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({ ok: false, status: 401, json: async () => ({ erro: 'x' }) })
    const { w } = await montar()
    await w.find('#email').setValue('a@b.com')
    await w.find('#senha').setValue('errada')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(w.find('[role=alert]').text()).toContain('incorretos')
  })

  it('login ok vai para conversas', async () => {
    const exp = new Date(Date.now() + 8 * 3600e3).toISOString()
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true, status: 200,
      json: async () => ({ token: 't', expiraEm: exp, usuario: { id: 1, nome: 'Ana' } })
    })
    const { w, router } = await montar()
    await w.find('#email').setValue('a@b.com')
    await w.find('#senha').setValue('12345678')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('conversas')
    expect(useAuthStore().logado).toBe(true)
  })
})
