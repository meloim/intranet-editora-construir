using System.Security.Claims;
using Intranet.Api.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.Auth;

public record LoginRequest(string? Email, string? Senha);

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrEmpty(req.Senha))
            return BadRequest(new { erro = "Informe e-mail e senha." });

        var email = req.Email.Trim().ToLowerInvariant();
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null || !BCrypt.Net.BCrypt.Verify(req.Senha, u.SenhaHash))
            return Unauthorized(new { erro = "E-mail ou senha inválidos." });
        if (!u.Ativo)
            return StatusCode(403, new { erro = "Conta desativada. Procure o administrador." });

        var (token, expira) = tokens.Gerar(u);
        return Ok(new { token, expiraEm = expira, usuario = Perfil(u) });
    }

    [Authorize]
    [HttpGet("eu")]
    public async Task<IActionResult> Eu()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var u = await db.Usuarios.FindAsync(id);
        if (u is null || !u.Ativo) return Unauthorized(new { erro = "Sessão inválida." });
        return Ok(Perfil(u));
    }

    private static object Perfil(Dominio.Usuario u) => new { u.Id, u.Nome, u.Email, u.Setor, u.Admin };
}
