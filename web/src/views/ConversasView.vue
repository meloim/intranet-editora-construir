<script setup>
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

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
    <p>A lista de conversas chega na próxima história.</p>
  </main>
</template>

<style scoped>
.conversas { padding: 16px; }
header { display: flex; justify-content: space-between; align-items: center; }
</style>
