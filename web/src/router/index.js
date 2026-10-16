import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import LoginView from '../views/LoginView.vue'
import ConversasView from '../views/ConversasView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: LoginView },
    { path: '/', name: 'conversas', component: ConversasView, meta: { privada: true } },
    { path: '/:pathMatch(.*)*', redirect: '/' }
  ]
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (to.meta.privada && !auth.logado) {
    const expirou = !!auth.token || !!localStorage.getItem('intranet.expirou')
    if (auth.token) localStorage.setItem('intranet.expirou', '1')
    auth.sair()
    return { name: 'login', query: expirou ? { expirada: '1' } : {} }
  }
  if (to.name === 'login' && auth.logado) return { name: 'conversas' }
})

export default router
