<script setup>
import { onMounted } from 'vue'
import { useContatosStore } from '../stores/contatos'

const emit = defineEmits(['abrir'])
const store = useContatosStore()

onMounted(() => store.carregar())

function selecionavel(c) {
  return c.ativo !== false || store.comHistorico.includes(c.id)
}
function abrir(c) {
  if (!selecionavel(c)) return
  emit('abrir', { contato: c, somenteLeitura: c.ativo === false })
}
function iniciais(nome) {
  return (nome || '?').split(' ').filter(Boolean).slice(0, 2).map((p) => p[0].toUpperCase()).join('')
}
</script>

<template>
  <section class="contatos" aria-label="Contatos do time">
    <p v-if="store.carregando" class="aviso">Carregando contatos…</p>
    <p v-else-if="store.erro" class="aviso" role="alert">{{ store.erro }}</p>
    <ul v-else>
      <li v-for="c in store.contatos" :key="c.id">
        <button
          type="button"
          class="contato"
          :class="{ inativo: c.ativo === false }"
          :disabled="!selecionavel(c)"
          :title="!selecionavel(c) ? 'Usuário inativo: não é possível iniciar conversa' : ''"
          @click="abrir(c)"
        >
          <span class="avatar">{{ iniciais(c.nome) }}</span>
          <span class="info">
            <strong>{{ c.nome }}</strong>
            <small>{{ c.setor }}</small>
          </span>
          <span v-if="c.ativo === false" class="etiqueta">inativo</span>
        </button>
      </li>
    </ul>
  </section>
</template>

<style scoped>
ul { list-style: none; margin: 0; padding: 0; }
.aviso { padding: 16px; color: #667781; }
.contato { display: flex; align-items: center; gap: 12px; width: 100%; padding: 10px 16px; background: #fff; border: 0; border-bottom: 1px solid #e9edef; text-align: left; cursor: pointer; font: inherit; }
.contato:hover:not(:disabled), .contato:focus-visible { background: var(--fundo); }
.contato:disabled { cursor: not-allowed; }
.avatar { width: 40px; height: 40px; border-radius: 50%; background: var(--verde); color: #fff; display: grid; place-items: center; font-weight: 600; flex: none; }
.info { display: flex; flex-direction: column; flex: 1; min-width: 0; }
.info small { color: #667781; }
.inativo { color: #8696a0; }
.inativo .avatar { background: #b0bac0; }
.etiqueta { font-size: 12px; background: #e9edef; color: #54656f; padding: 2px 8px; border-radius: 10px; }
</style>
