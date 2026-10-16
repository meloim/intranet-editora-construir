using System.Security.Claims;
using System.Text.RegularExpressions;
using Intranet.Api.Dados;
using Intranet.Api.Dominio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.Contas;

public record CriarUsuarioRequest(string? Nome, string? Email, string? Senha, string? Setor, bool Admin = false);

[ApiController]
[Authorize]
public class UsuariosController(AppDbContext db, Chat.PresencaService presenca) : ControllerBase
{
    private async Task<Usuario?> Logado()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var u = await db.Usuarios.FindAsync(id);
        return u is { Ativo: true } ? u : null;
    }

    private object Item(Usuario u) =>
        new { u.Id, u.Nome, u.Email, u.Setor, AvatarUrl = (string?)null, Online = presenca.EstaOnline(u.Id), u.Ativo, u.Admin };

    [HttpGet("api/contatos")]
    public async Task<IActionResult> Contatos()
    {
        var eu = await Logado();
        if (eu is null) return Unauthorized(new { erro = "Sessão inválida." });
        var lista = await db.Usuarios.Where(x => x.Id != eu.Id).OrderBy(x => x.Nome).ToListAsync();
        return Ok(lista.Select(Item));
    }

    [HttpGet("api/usuarios")]
    public async Task<IActionResult> Listar()
    {
        var eu = await Logado();
        if (eu is null) return Unauthorized(new { erro = "Sessão inválida." });
        if (!eu.Admin) return StatusCode(403, new { erro = "Apenas o administrador." });
        return Ok((await db.Usuarios.OrderBy(x => x.Nome).ToListAsync()).Select(Item));
    }

    [HttpPost("api/usuarios")]
    public async Task<IActionResult> Criar(CriarUsuarioRequest req)
    {
        var eu = await Logado();
        if (eu is null) return Unauthorized(new { erro = "Sessão inválida." });
        if (!eu.Admin) return StatusCode(403, new { erro = "Apenas o administrador." });

        if (string.IsNullOrWhiteSpace(req.Nome) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrEmpty(req.Senha))
            return BadRequest(new { erro = "Informe nome, e-mail e senha inicial." });
        var email = req.Email.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return BadRequest(new { erro = "E-mail inválido." });
        if (req.Senha.Length < 8)
            return BadRequest(new { erro = "A senha deve ter no mínimo 8 caracteres." });
        if (await db.Usuarios.AnyAsync(x => x.Email == email))
            return Conflict(new { erro = "Já existe uma conta com esse e-mail." });

        var novo = new Usuario
        {
            Nome = req.Nome.Trim(), Email = email, Setor = req.Setor?.Trim(), Admin = req.Admin,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.Senha),
        };
        db.Usuarios.Add(novo);
        await db.SaveChangesAsync();
        return Created($"api/usuarios/{novo.Id}", Item(novo));
    }

    [HttpPost("api/usuarios/{id:int}/desativar")]
    public Task<IActionResult> Desativar(int id) => Alterar(id, false);

    [HttpPost("api/usuarios/{id:int}/ativar")]
    public Task<IActionResult> Ativar(int id) => Alterar(id, true);

    private async Task<IActionResult> Alterar(int id, bool ativo)
    {
        var eu = await Logado();
        if (eu is null) return Unauthorized(new { erro = "Sessão inválida." });
        if (!eu.Admin) return StatusCode(403, new { erro = "Apenas o administrador." });
        if (!ativo && id == eu.Id)
            return BadRequest(new { erro = "Você não pode desativar a própria conta." });
        var u = await db.Usuarios.FindAsync(id);
        if (u is null) return NotFound(new { erro = "Usuário não encontrado." });
        u.Ativo = ativo;
        await db.SaveChangesAsync();
        return Ok(Item(u));
    }
}
