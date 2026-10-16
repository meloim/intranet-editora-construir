using System.Security.Claims;
using Intranet.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.Chat;

public record TextoRequest(string? Texto);

[ApiController]
[Authorize]
public class ChatController(AppDbContext db, ChatService chat) : ControllerBase
{
    private int MeuId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<bool> SessaoValida() => (await db.Usuarios.FindAsync(MeuId)) is { Ativo: true };

    // Lista de conversas do usuário. id = id do colega (a conversa 1:1 é identificada por ele).
    [HttpGet("api/conversas")]
    public async Task<IActionResult> Conversas()
    {
        if (!await SessaoValida()) return Unauthorized(new { erro = "Sessão inválida." });
        var eu = MeuId;
        var msgs = await db.Mensagens.Where(m => m.RemetenteId == eu || m.DestinatarioId == eu).ToListAsync();
        var outros = msgs.GroupBy(m => m.RemetenteId == eu ? m.DestinatarioId : m.RemetenteId).ToList();
        var ids = outros.Select(g => g.Key).ToList();
        var nomes = await db.Usuarios.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nome);
        var lista = outros.Select(g =>
        {
            var ult = g.OrderByDescending(m => m.EnviadaEm).ThenByDescending(m => m.Id).First();
            return new
            {
                id = g.Key, nome = nomes[g.Key], ultimaMensagem = ChatService.Dto(ult).Texto,
                hora = ChatService.Dto(ult).EnviadaEm, naoLidas = 0,
            };
        }).OrderByDescending(x => x.hora);
        return Ok(lista);
    }

    // Histórico paginado: sem parâmetros devolve as últimas `limite` mensagens (padrão 50, máx. 100).
    // `antes` = id da mensagem mais antiga já carregada; devolve as anteriores a ela.
    // Resposta sempre em ordem cronológica; header X-Tem-Mais indica se há mensagens mais antigas.
    [HttpGet("api/conversas/{contatoId:int}/mensagens")]
    public async Task<IActionResult> Mensagens(int contatoId, [FromQuery] int? antes, [FromQuery] int limite = 50)
    {
        if (!await SessaoValida()) return Unauthorized(new { erro = "Sessão inválida." });
        var eu = MeuId;
        limite = Math.Clamp(limite, 1, 100);
        var q = db.Mensagens.Where(m => (m.RemetenteId == eu && m.DestinatarioId == contatoId) ||
                                        (m.RemetenteId == contatoId && m.DestinatarioId == eu));
        if (antes is not null)
        {
            var ref_ = await q.Where(m => m.Id == antes).Select(m => new { m.EnviadaEm, m.Id }).FirstOrDefaultAsync();
            if (ref_ is null) return NotFound(new { erro = "Mensagem de referência não encontrada." });
            q = q.Where(m => m.EnviadaEm < ref_.EnviadaEm || (m.EnviadaEm == ref_.EnviadaEm && m.Id < ref_.Id));
        }
        var pagina = await q.OrderByDescending(m => m.EnviadaEm).ThenByDescending(m => m.Id)
            .Take(limite + 1).ToListAsync();
        var temMais = pagina.Count > limite;
        pagina = pagina.Take(limite).Reverse().ToList();
        Response.Headers["X-Tem-Mais"] = temMais ? "true" : "false";
        return Ok(pagina.Select(ChatService.Dto));
    }

    [HttpPost("api/conversas/{contatoId:int}/mensagens")]
    public async Task<IActionResult> Enviar(int contatoId, TextoRequest req)
    {
        if (!await SessaoValida()) return Unauthorized(new { erro = "Sessão inválida." });
        var (dto, erro) = await chat.Enviar(MeuId, contatoId, req.Texto);
        if (erro is null) return Created($"api/conversas/{contatoId}/mensagens", dto);
        return erro.StartsWith("Colega não") ? NotFound(new { erro }) : BadRequest(new { erro });
    }

    [HttpPut("api/mensagens/{id:int}")]
    public async Task<IActionResult> Editar(int id, TextoRequest req)
    {
        if (!await SessaoValida()) return Unauthorized(new { erro = "Sessão inválida." });
        var (st, dto, erro) = await chat.Editar(MeuId, id, req.Texto);
        return st == 200 ? Ok(dto) : StatusCode(st, new { erro });
    }

    [HttpDelete("api/mensagens/{id:int}")]
    public async Task<IActionResult> Apagar(int id)
    {
        if (!await SessaoValida()) return Unauthorized(new { erro = "Sessão inválida." });
        var (st, dto, erro) = await chat.Apagar(MeuId, id);
        return st == 200 ? Ok(dto) : StatusCode(st, new { erro });
    }
}
