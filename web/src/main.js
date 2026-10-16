import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { definirAoSessaoInvalida } from './services/api'
import { useAuthStore } from './stores/auth'
import './style.css'

const pinia = createPinia()
const app = createApp(App).use(pinia).use(router)

// 401 em chamada autenticada: sessão inválida, volta para o login com o aviso de expirada
definirAoSessaoInvalida(() => {
  localStorage.setItem('intranet.expirou', '1')
  useAuthStore().sair()
  router.push({ name: 'login', query: { expirada: '1' } })
})

app.mount('#app')
