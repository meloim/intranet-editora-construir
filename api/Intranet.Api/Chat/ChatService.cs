using Intranet.Api.Dados;
using Intranet.Api.Dominio;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.Chat;

public record MensagemDto(int Id, int RemetenteId, int DestinatarioId, string Texto, DateTime EnviadaEm,
    bool Editada, bool Apagada, bool Lida = false);

public class ChatService(AppDbContext db, IHubContext<ChatHub> hub)
{
    public const string TextoApagada = "mensagem apagada";
    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(15);
    public const int MaxTexto = 4000;

    public static MensagemDto Dto(Mensagem m) => new(m.Id, m.RemetenteId, m.DestinatarioId,
        m.ApagadaEm is null ? m.Texto : TextoApagada, DateTime.SpecifyKind(m.EnviadaEm, DateTimeKind.Utc),
        m.EditadaEm is not null, m.ApagadaEm is not null, m.LidaEm is not null);

    private Task Notificar(Mensagem m, string evento) =>
        hub.Clients.Users(m.RemetenteId.ToString(), m.DestinatarioId.ToString()).SendAsync(evento, Dto(m));

    public async Task<(MensagemDto? Dto, string? Erro)> Enviar(int de, int para, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, "Escreva uma mensagem.");
        if (texto.Length > MaxTexto) return (null, $"A mensagem pode ter no máximo {MaxTexto} caracteres.");
        if (de == para) return (null, "Escolha um colega para conversar.");
        var remetente = await db.Usuarios.FindAsync(de);
        if (remetente is not { Ativo: true }) return (null, "Sessão inválida.");
        var dest = await db.Usuarios.FindAsync(para);
        if (dest is null) return (null, "Colega não encontrado.");
        if (!dest.Ativo) return (null, "Este colega está inativo e não recebe mensagens.");

        var m = new Mensagem { RemetenteId = de, DestinatarioId = para, Texto = texto.Trim() };
        db.Mensagens.Add(m);
        await db.SaveChangesAsync(); // salva antes de entregar
        await Notificar(m, "mensagemRecebida");
        return (Dto(m), null);
    }

    // Marca como lidas as mensagens que `contatoId` enviou para `leitorId` e avisa quem enviou
    // (evento "mensagensLidas" {leitorId, ids, lidaEm}). Devolve os ids marcados agora.
    public async Task<List<int>> MarcarLidas(int leitorId, int contatoId)
    {
        var pend = await db.Mensagens.Where(m => m.RemetenteId == contatoId && m.DestinatarioId == leitorId
                                                  && m.LidaEm == null).ToListAsync();
        if (pend.Count == 0) return [];
        var agora = DateTime.UtcNow;
        foreach (var m in pend) m.LidaEm = agora;
        await db.SaveChangesAsync();
        var ids = pend.Select(m => m.Id).ToList();
        await hub.Clients.Users(contatoId.ToString(), leitorId.ToString())
            .SendAsync("mensagensLidas", new { leitorId, ids, lidaEm = DateTime.SpecifyKind(agora, DateTimeKind.Utc) });
        return ids;
    }

    // status: 200 ok, 403 não é sua / fora do prazo, 404 não existe, 400 inválido
    public async Task<(int Status, MensagemDto? Dto, string? Erro)> Editar(int usuarioId, int id, string? texto)
    {
        var m = await db.Mensagens.FindAsync(id);
        if (m is null || (m.RemetenteId != usuarioId && m.DestinatarioId != usuarioId))
            return (404, null, "Mensagem não encontrada.");
        if (m.RemetenteId != usuarioId) return (403, null, "Você só pode editar suas próprias mensagens.");
        if (m.ApagadaEm is not null) return (400, null, "Mensagem apagada não pode ser editada.");
        if (DateTime.UtcNow - m.EnviadaEm > Janela) return (403, null, "O prazo de 15 minutos para editar acabou.");
        if (string.IsNullOrWhiteSpace(texto)) return (400, null, "Escreva uma mensagem.");
        if (texto.Length > MaxTexto) return (400, null, $"A mensagem pode ter no máximo {MaxTexto} caracteres.");
        m.Texto = texto.Trim();
        m.EditadaEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await Notificar(m, "mensagemEditada");
        return (200, Dto(m), null);
    }

    public async Task<(int Status, MensagemDto? Dto, string? Erro)> Apagar(int usuarioId, int id)
    {
        var m = await db.Mensagens.FindAsync(id);
        if (m is null || (m.RemetenteId != usuarioId && m.DestinatarioId != usuarioId))
            return (404, null, "Mensagem não encontrada.");
        if (m.RemetenteId != usuarioId) return (403, null, "Você só pode apagar suas próprias mensagens.");
        if (m.ApagadaEm is not null) return (200, Dto(m), null);
        if (DateTime.UtcNow - m.EnviadaEm > Janela) return (403, null, "O prazo de 15 minutos para apagar acabou.");
        m.ApagadaEm = DateTime.UtcNow; // texto original mantido no banco; a API nunca o devolve
        await db.SaveChangesAsync();
        await Notificar(m, "mensagemApagada");
        return (200, Dto(m), null);
    }
}
