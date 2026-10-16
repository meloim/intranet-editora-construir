using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Intranet.Api.Dominio;
using Microsoft.IdentityModel.Tokens;

namespace Intranet.Api.Auth;

public class TokenService(IConfiguration config)
{
    public const int HorasSessao = 8;

    public static SymmetricSecurityKey Chave(IConfiguration c) =>
        new(Encoding.UTF8.GetBytes(c["Jwt:Chave"] ?? throw new InvalidOperationException("Jwt:Chave ausente")));

    public (string token, DateTime expira) Gerar(Usuario u)
    {
        var expira = DateTime.UtcNow.AddHours(HorasSessao);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new Claim(ClaimTypes.Name, u.Nome),
            new Claim(ClaimTypes.Email, u.Email),
            new Claim(ClaimTypes.Role, u.Admin ? "admin" : "usuario"),
        };
        var jwt = new JwtSecurityToken("intranet", "intranet", claims, expires: expira,
            signingCredentials: new SigningCredentials(Chave(config), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }
}
