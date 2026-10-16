<script setup>
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import ContatosLista from './ContatosLista.vue'

const auth = useAuthStore()
const router = useRouter()
function sair() {
  auth.sair()
  router.push({ name: 'login' })
}
</script>

<template>
  <main class="conversas">
    <header>
      <strong>Olá, {{ auth.usuario?.nome }}</strong>
      <button @click="sair">Sair</button>
    </header>
    <ContatosLista />
  </main>
</template>

<style scoped>
.conversas { padding: 16px; }
header { display: flex; justify-content: space-between; align-items: center; }
</style>
