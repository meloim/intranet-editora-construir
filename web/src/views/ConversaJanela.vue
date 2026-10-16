<script setup>
import { ref, watch, nextTick, onMounted, onUnmounted } from 'vue'
import { useChatStore, dentroDoPrazo } from '../stores/chat'
import { useAuthStore } from '../stores/auth'

defineEmits(['voltar'])
const chat = useChatStore()
const auth = useAuthStore()
const texto = ref('')
const editandoId = ref(null)
const rolagem = ref(null)
const agora = ref(Date.now())
let timer

// atualiza o relógio para os botões de editar/apagar sumirem ao fim dos 15 minutos
onMounted(() => { timer = setInterval(() => { agora.value = Date.now() }, 15000) })
onUnmounted(() => clearInterval(timer))

const minha = (m) => m.remetenteId === auth.usuario?.id
const podeMexer = (m) => minha(m) && !m.apagada && !chat.somenteLeitura && dentroDoPrazo(m, agora.value)
const hora = (iso) => new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })

async function rolarFim() {
  await nextTick()
  if (rolagem.value) rolagem.value.scrollTop = rolagem.value.scrollHeight
}
watch(() => chat.contato?.id, rolarFim)
watch(() => chat.mensagens.length, (novo, velho) => {
  // novas no fim -> rola; antigas no início (prepend) -> mantém a posição
  if (!velho || chat.mensagens.at(-1)?.id !== ultimaVista) rolarFim()
  ultimaVista = chat.mensagens.at(-1)?.id
})
let ultimaVista = null

async function aoRolar(e) {
  if (e.target.scrollTop > 40) return
  const el = rolagem.value
  const antes = el.scrollHeight
  await chat.carregarAntigas()
  await nextTick()
  el.scrollTop = el.scrollHeight - antes
}

function iniciarEdicao(m) {
  editandoId.value = m.id
  texto.value = m.texto
}
function cancelar() {
  editandoId.value = null
  texto.value = ''
}
async function enviar() {
  const t = texto.value.trim()
  if (!t) return
  try {
    if (editandoId.value) await chat.editar(editandoId.value, t)
    else await chat.enviar(t)
    cancelar()
  } catch { /* erro já exibido pela store */ }
}
async function apagar(m) {
  if (!confirm('Apagar esta mensagem?')) return
  try { await chat.apagar(m.id) } catch { /* erro exibido */ }
}
</script>

<template>
  <section class="janela" v-if="chat.contato" :aria-label="`Conversa com ${chat.contato.nome}`">
    <header>
      <button class="voltar" type="button" aria-label="Voltar para a lista" @click="$emit('voltar')">←</button>
      <strong>{{ chat.contato.nome }}</strong>
      <small v-if="chat.somenteLeitura">inativo · somente leitura</small>
    </header>

    <div ref="rolagem" class="mensagens" @scroll="aoRolar">
      <p v-if="chat.carregando && !chat.mensagens.length" class="aviso">Carregando…</p>
      <p v-else-if="!chat.mensagens.length" class="aviso">Nenhuma mensagem ainda. Diga oi!</p>
      <div v-for="m in chat.mensagens" :key="m.id" class="linha" :class="{ minha: minha(m) }">
        <div class="balao" :class="{ apagada: m.apagada }">
          <span class="texto">{{ m.apagada ? 'mensagem apagada' : m.texto }}</span>
          <span class="meta">
            <em v-if="m.editada && !m.apagada">editada</em>
            {{ hora(m.enviadaEm) }}
          </span>
          <span v-if="podeMexer(m)" class="acoes">
            <button type="button" @click="iniciarEdicao(m)">Editar</button>
            <button type="button" @click="apagar(m)">Apagar</button>
          </span>
        </div>
      </div>
    </div>

    <p v-if="chat.erro" class="erro" role="alert">{{ chat.erro }}</p>
    <p v-if="!chat.conectado" class="offline">Reconectando ao tempo real…</p>

    <form v-if="!chat.somenteLeitura" class="envio" @submit.prevent="enviar">
      <span v-if="editandoId" class="editando">Editando mensagem <button type="button" @click="cancelar">cancelar</button></span>
      <input v-model="texto" type="text" placeholder="Digite uma mensagem" aria-label="Mensagem" maxlength="4000" />
      <button type="submit" :disabled="!texto.trim()">{{ editandoId ? 'Salvar' : 'Enviar' }}</button>
    </form>
  </section>
  <section v-else class="janela vazia"><p>Escolha um colega para conversar.</p></section>
</template>

<style scoped>
.janela { display: flex; flex-direction: column; height: 100%; background: #efeae2; min-height: 0; }
.vazia { align-items: center; justify-content: center; color: #667781; }
header { display: flex; align-items: center; gap: 10px; padding: 10px 16px; background: #f0f2f5; border-bottom: 1px solid #d1d7db; }
header small { color: #667781; }
.voltar { display: none; border: 0; background: none; font-size: 22px; cursor: pointer; }
.mensagens { flex: 1; overflow-y: auto; padding: 12px 16px; display: flex; flex-direction: column; gap: 6px; }
.aviso { text-align: center; color: #667781; }
.linha { display: flex; }
.linha.minha { justify-content: flex-end; }
.balao { max-width: min(75%, 480px); background: #fff; padding: 6px 10px; border-radius: 8px; box-shadow: 0 1px 1px rgba(0,0,0,.1); overflow-wrap: anywhere; }
.minha .balao { background: #d9fdd3; }
.balao.apagada .texto { font-style: italic; color: #667781; }
.meta { display: block; text-align: right; font-size: 11px; color: #667781; }
.meta em { margin-right: 6px; }
.acoes { display: flex; gap: 8px; justify-content: flex-end; }
.acoes button { border: 0; background: none; color: #027eb5; font-size: 12px; cursor: pointer; padding: 2px; }
.erro { margin: 0; padding: 6px 16px; background: #fde8e8; color: #b42318; }
.offline { margin: 0; padding: 4px 16px; background: #fff4d6; color: #7a5b00; font-size: 12px; }
.envio { display: flex; flex-wrap: wrap; gap: 8px; padding: 10px 16px; background: #f0f2f5; }
.envio input { flex: 1; min-width: 0; padding: 10px 14px; border: 0; border-radius: 20px; font: inherit; }
.envio button[type=submit] { border: 0; border-radius: 20px; padding: 0 18px; background: var(--verde); color: #fff; cursor: pointer; }
.envio button:disabled { opacity: .5; cursor: default; }
.editando { width: 100%; font-size: 12px; color: #54656f; }
@media (max-width: 700px) { .voltar { display: block; } }
</style>
