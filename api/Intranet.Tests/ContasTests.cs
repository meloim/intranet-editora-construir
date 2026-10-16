using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intranet.Api.Dados;
using Microsoft.Extensions.DependencyInjection;

namespace Intranet.Tests;

public class ContasTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private void CriarAdmin(string email)
    {
        f.CriarUsuario(email, "senha1234");
        using var s = f.Services.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Usuarios.Single(u => u.Email == email).Admin = true;
        db.SaveChanges();
    }

    private async Task<HttpClient> Cliente(string email, string senha = "senha1234")
    {
        var r = await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, senha });
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return c;
    }

    [Fact]
    public async Task Cadastro_sem_login_e_negado() =>
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await f.CreateClient().PostAsJsonAsync("/api/usuarios", new { nome = "X", email = "x@e.com", senha = "12345678" })).StatusCode);

    [Fact]
    public async Task Nao_admin_nao_cria_conta()
    {
        f.CriarUsuario("comum1@e.com", "senha1234");
        var c = await Cliente("comum1@e.com");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await c.PostAsJsonAsync("/api/usuarios", new { nome = "X", email = "x@e.com", senha = "12345678" })).StatusCode);
    }

    [Fact]
    public async Task Admin_cria_conta_e_novo_usuario_loga()
    {
        CriarAdmin("adm1@e.com");
        var c = await Cliente("adm1@e.com");
        var r = await c.PostAsJsonAsync("/api/usuarios", new { nome = "Novo", email = "Novo@E.com", senha = "inicial123" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "novo@e.com", senha = "inicial123" })).StatusCode);
    }

    [Theory]
    [InlineData("", "a@e.com", "12345678")]
    [InlineData("A", "", "12345678")]
    [InlineData("A", "a@e.com", "")]
    [InlineData("A", "invalido", "12345678")]
    [InlineData("A", "a@e.com", "curta")]
    public async Task Criar_conta_valida_campos(string nome, string email, string senha)
    {
        var adm = $"adm-{Guid.NewGuid():N}@e.com";
        CriarAdmin(adm);
        var c = await Cliente(adm);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await c.PostAsJsonAsync("/api/usuarios", new { nome, email, senha })).StatusCode);
    }

    [Fact]
    public async Task Email_duplicado_retorna_409()
    {
        CriarAdmin("adm3@e.com");
        f.CriarUsuario("dup@e.com", "senha1234");
        var c = await Cliente("adm3@e.com");
        Assert.Equal(HttpStatusCode.Conflict,
            (await c.PostAsJsonAsync("/api/usuarios", new { nome = "D", email = "dup@e.com", senha = "12345678" })).StatusCode);
    }

    [Fact]
    public async Task Desativar_bloqueia_login_e_sessao_e_continua_nos_contatos_como_inativo()
    {
        CriarAdmin("adm4@e.com");
        f.CriarUsuario("alvo@e.com", "senha1234");
        var alvo = await Cliente("alvo@e.com");
        var admin = await Cliente("adm4@e.com");

        var lista = await admin.GetFromJsonAsync<JsonElement>("/api/contatos");
        var id = lista.EnumerateArray().Single(x => x.GetProperty("email").GetString() == "alvo@e.com").GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/usuarios/{id}/desativar", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "alvo@e.com", senha = "senha1234" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await alvo.GetAsync("/api/contatos")).StatusCode);

        lista = await admin.GetFromJsonAsync<JsonElement>("/api/contatos");
        var item = lista.EnumerateArray().Single(x => x.GetProperty("email").GetString() == "alvo@e.com");
        Assert.False(item.GetProperty("ativo").GetBoolean());
    }

    [Fact]
    public async Task Contatos_nao_inclui_o_proprio_usuario()
    {
        f.CriarUsuario("eu1@e.com", "senha1234");
        var c = await Cliente("eu1@e.com");
        var lista = await c.GetFromJsonAsync<JsonElement>("/api/contatos");
        Assert.DoesNotContain(lista.EnumerateArray(), x => x.GetProperty("email").GetString() == "eu1@e.com");
    }

    [Fact]
    public async Task Admin_nao_desativa_a_si_mesmo_e_comum_nao_desativa()
    {
        CriarAdmin("adm5@e.com");
        f.CriarUsuario("comum2@e.com", "senha1234");
        var admin = await Cliente("adm5@e.com");
        var comum = await Cliente("comum2@e.com");
        var meuId = (await admin.GetFromJsonAsync<JsonElement>("/api/auth/eu")).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/usuarios/{meuId}/desativar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await comum.PostAsync($"/api/usuarios/{meuId}/desativar", null)).StatusCode);
    }
}
