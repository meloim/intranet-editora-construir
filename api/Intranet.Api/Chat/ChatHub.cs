using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Intranet.Api.Chat;

// Eventos para o cliente: "mensagemRecebida", "mensagemEditada", "mensagemApagada" (payload = MensagemDto).
// Cada usuário recebe apenas o que é enviado ao seu UserIdentifier (id do usuário).
[Authorize]
public class ChatHub(ChatService chat, PresencaService presenca) : Hub
{
    // Evento "presenca" {usuarioId, online} para todos quando alguém entra/sai (várias abas contam como uma).
    public override async Task OnConnectedAsync()
    {
        var id = int.Parse(Context.UserIdentifier!);
        if (presenca.Conectou(id)) await Clients.All.SendAsync("presenca", new { usuarioId = id, online = true });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var id = int.Parse(Context.UserIdentifier!);
        if (presenca.Desconectou(id)) await Clients.All.SendAsync("presenca", new { usuarioId = id, online = false });
        await base.OnDisconnectedAsync(exception);
    }

    public Task MarcarComoLida(int contatoId) => chat.MarcarLidas(int.Parse(Context.UserIdentifier!), contatoId);

    public async Task EnviarMensagem(int destinatarioId, string texto)
    {
        var (dto, erro) = await chat.Enviar(int.Parse(Context.UserIdentifier!), destinatarioId, texto);
        if (erro is not null) throw new HubException(erro);
    }
}
