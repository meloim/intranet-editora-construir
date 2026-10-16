using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Intranet.Api.Chat;

// Eventos para o cliente: "mensagemRecebida", "mensagemEditada", "mensagemApagada" (payload = MensagemDto).
// Cada usuário recebe apenas o que é enviado ao seu UserIdentifier (id do usuário).
[Authorize]
public class ChatHub(ChatService chat) : Hub
{
    public async Task EnviarMensagem(int destinatarioId, string texto)
    {
        var (dto, erro) = await chat.Enviar(int.Parse(Context.UserIdentifier!), destinatarioId, texto);
        if (erro is not null) throw new HubException(erro);
    }
}
