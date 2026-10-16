using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intranet.Api.Dados;
using Intranet.Api.Dominio;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Intranet.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("UsarBancoEmMemoria", "true")
               .UseSetting("Jwt:Chave", "chave-de-teste-com-mais-de-trinta-e-dois-caracteres");

    public void CriarUsuario(string email, string senha, bool ativo = true)
    {
        using var s = Services.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Usuarios.Add(new Usuario { Nome = "Teste", Email = email, Ativo = ativo,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(senha) });
        db.SaveChanges();
    }
}

public class AuthTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private Task<HttpResponseMessage> Login(string email, string senha) =>
        f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, senha });

    private async Task<HttpClient> ClienteLogado(string email, string senha)
    {
        var json = await (await Login(email, senha)).Content.ReadFromJsonAsync<JsonElement>();
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return c;
    }

    [Fact]
    public async Task Login_valido_retorna_token_de_8_horas()
    {
        f.CriarUsuario("ana@editora.com", "senha1234");
        var r = await Login("ANA@editora.com", "senha1234");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(json.GetProperty("token").GetString());
        Assert.InRange((jwt.ValidTo - DateTime.UtcNow).TotalHours, 7.9, 8.0);
    }

    [Fact]
    public async Task Senha_errada_retorna_401()
    {
        f.CriarUsuario("bia@editora.com", "senha1234");
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login("bia@editora.com", "errada")).StatusCode);
    }

    [Fact]
    public async Task Usuario_inexistente_retorna_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login("nao@editora.com", "x")).StatusCode);

    [Fact]
    public async Task Usuario_desativado_nao_loga()
    {
        f.CriarUsuario("cai@editora.com", "senha1234", ativo: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await Login("cai@editora.com", "senha1234")).StatusCode);
    }

    [Fact]
    public async Task Campos_vazios_retornam_400() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Login("", "")).StatusCode);

    [Fact]
    public async Task Eu_exige_token_e_funciona_com_token()
    {
        f.CriarUsuario("duda@editora.com", "senha1234");
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/auth/eu")).StatusCode);
        var c = await ClienteLogado("duda@editora.com", "senha1234");
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/auth/eu")).StatusCode);
    }

    [Fact]
    public async Task Eu_retorna_401_se_usuario_foi_desativado_depois_do_login()
    {
        f.CriarUsuario("edu@editora.com", "senha1234");
        var c = await ClienteLogado("edu@editora.com", "senha1234");
        using (var s = f.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Usuarios.Single(u => u.Email == "edu@editora.com").Ativo = false;
            db.SaveChanges();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/eu")).StatusCode);
    }
}
