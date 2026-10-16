<script setup>
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const email = ref('')
const senha = ref('')
const erro = ref('')
const carregando = ref(false)
const expirada = route.query.expirada === '1'

async function entrar() {
  erro.value = ''
  if (!email.value.trim() || !senha.value) {
    erro.value = 'Preencha o e-mail e a senha.'
    return
  }
  carregando.value = true
  try {
    await auth.entrar(email.value.trim(), senha.value)
    router.push({ name: 'conversas' })
  } catch (e) {
    erro.value = e.status === 401 ? 'E-mail ou senha incorretos.'
      : e.status === 403 ? 'Esta conta está desativada. Fale com o administrador.'
      : e.message
  } finally {
    carregando.value = false
  }
}
</script>

<template>
  <main class="tela">
    <form class="cartao" @submit.prevent="entrar" novalidate>
      <h1>Intranet Construir</h1>
      <p class="sub">Entre com seu e-mail da empresa</p>

      <p v-if="expirada && !erro" class="aviso" role="status">
        Sua sessão expirou. Entre novamente.
      </p>

      <label for="email">E-mail</label>
      <input id="email" v-model="email" type="email" autocomplete="username" inputmode="email" />

      <label for="senha">Senha</label>
      <input id="senha" v-model="senha" type="password" autocomplete="current-password" />

      <p v-if="erro" class="erro" role="alert">{{ erro }}</p>

      <button type="submit" :disabled="carregando">
        {{ carregando ? 'Entrando…' : 'Entrar' }}
      </button>
    </form>
  </main>
</template>

<style scoped>
.tela {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  background: linear-gradient(var(--verde) 0 35%, var(--fundo) 35% 100%);
}
.cartao {
  width: 100%;
  max-width: 400px;
  background: #fff;
  padding: 32px 28px;
  border-radius: 12px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.12);
  display: flex;
  flex-direction: column;
}
h1 { margin: 0; font-size: 1.5rem; color: var(--verde); }
.sub { margin: 4px 0 20px; color: #667781; }
label { font-size: 0.85rem; font-weight: 600; margin: 12px 0 4px; }
input {
  font: inherit;
  padding: 12px;
  border: 1px solid #ccd0d5;
  border-radius: 8px;
}
input:focus { outline: 2px solid var(--verde); border-color: var(--verde); }
button {
  margin-top: 20px;
  padding: 12px;
  font: inherit;
  font-weight: 600;
  color: #fff;
  background: var(--verde);
  border: 0;
  border-radius: 8px;
  cursor: pointer;
}
button:hover:not(:disabled) { background: var(--verde-escuro); }
button:disabled { opacity: 0.6; cursor: wait; }
.erro { color: var(--erro); background: #fdecea; padding: 10px; border-radius: 8px; margin: 14px 0 0; font-size: 0.9rem; }
.aviso { color: #7a5b00; background: #fff4d6; padding: 10px; border-radius: 8px; margin: 0 0 8px; font-size: 0.9rem; }
@media (max-width: 480px) {
  .cartao { padding: 24px 20px; }
}
</style>
