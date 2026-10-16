<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useChatStore } from '../stores/chat'
import ContatosLista from './ContatosLista.vue'
import ConversaJanela from './ConversaJanela.vue'

const auth = useAuthStore()
const chat = useChatStore()
const router = useRouter()
const aberta = ref(false) // no celular: mostra a conversa em vez da lista

onMounted(() => chat.conectar())
onUnmounted(() => chat.desconectar())

function abrir({ contato, somenteLeitura }) {
  chat.abrir(contato, somenteLeitura)
  aberta.value = true
}
function sair() {
  chat.desconectar()
  auth.sair()
  router.push({ name: 'login' })
}
</script>

<template>
  <main class="conversas" :class="{ aberta }">
    <aside class="lateral">
      <header>
        <strong>Olá, {{ auth.usuario?.nome }}</strong>
        <button @click="sair">Sair</button>
      </header>
      <ContatosLista @abrir="abrir" />
    </aside>
    <div class="principal">
      <ConversaJanela @voltar="aberta = false" />
    </div>
  </main>
</template>

<style scoped>
.conversas { display: grid; grid-template-columns: 340px 1fr; height: 100vh; }
.lateral { display: flex; flex-direction: column; border-right: 1px solid #d1d7db; background: #fff; overflow-y: auto; min-height: 0; }
.lateral header { display: flex; justify-content: space-between; align-items: center; padding: 12px 16px; background: #f0f2f5; }
.principal { min-width: 0; min-height: 0; }
@media (max-width: 700px) {
  .conversas { grid-template-columns: 1fr; }
  .principal { display: none; }
  .conversas.aberta .principal { display: block; }
  .conversas.aberta .lateral { display: none; }
}
</style>
