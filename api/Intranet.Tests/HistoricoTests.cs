using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intranet.Api.Dados;
using Microsoft.Extensions.DependencyInjection;

namespace Intranet.Tests;

public class HistoricoTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private record Sessao(HttpClient Http, int Id);

    private async Task<Sessao> Entrar(string email)
    {
        f.CriarUsuario(email, "senha1234");
        var r = await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, senha = "senha1234" });
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return new Sessao(c, json.GetProperty("usuario").GetProperty("id").GetInt32());
    }

    private static async Task<(int[] Ids, bool TemMais)> Pagina(Sessao s, int outro, string query)
    {
        var r = await s.Http.GetAsync($"/api/conversas/{outro}/mensagens{query}");
        r.EnsureSuccessStatusCode();
        var arr = await r.Content.ReadFromJsonAsync<JsonElement>();
        return (arr.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToArray(),
            r.Headers.GetValues("X-Tem-Mais").First() == "true");
    }

    [Fact]
    public async Task Pagina_ultimas_e_depois_as_anteriores_em_ordem_cronologica()
    {
        var a = await Entrar("hist1a@t.com"); var b = await Entrar("hist1b@t.com");
        var enviados = new List<int>();
        for (var i = 0; i < 7; i++)
        {
            var r = await a.Http.PostAsJsonAsync($"/api/conversas/{b.Id}/mensagens", new { texto = $"m{i}" });
            enviados.Add((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32());
        }
        var (p1, mais1) = await Pagina(b, a.Id, "?limite=3");
        Assert.Equal(enviados.Skip(4).ToArray(), p1); Assert.True(mais1);
        var (p2, mais2) = await Pagina(b, a.Id, $"?limite=3&antes={p1[0]}");
        Assert.Equal(enviados.Skip(1).Take(3).ToArray(), p2); Assert.True(mais2);
        var (p3, mais3) = await Pagina(b, a.Id, $"?limite=3&antes={p2[0]}");
        Assert.Equal(new[] { enviados[0] }, p3); Assert.False(mais3);
    }

    [Fact]
    public async Task Antes_inexistente_ou_de_outra_conversa_da_404()
    {
        var a = await Entrar("hist2a@t.com"); var b = await Entrar("hist2b@t.com"); var c = await Entrar("hist2c@t.com");
        var r = await a.Http.PostAsJsonAsync($"/api/conversas/{b.Id}/mensagens", new { texto = "x" });
        var id = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await c.Http.GetAsync($"/api/conversas/{a.Id}/mensagens?antes={id}")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await a.Http.GetAsync($"/api/conversas/{b.Id}/mensagens?antes=99999")).StatusCode);
    }

    [Fact]
    public async Task Desativar_usuario_nao_apaga_mensagens_e_historico_continua()
    {
        var a = await Entrar("hist3a@t.com"); var b = await Entrar("hist3b@t.com");
        await a.Http.PostAsJsonAsync($"/api/conversas/{b.Id}/mensagens", new { texto = "oi" });
        await b.Http.PostAsJsonAsync($"/api/conversas/{a.Id}/mensagens", new { texto = "olá" });
        using (var s = f.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Usuarios.Find(b.Id)!.Ativo = false;
            db.SaveChanges();
        }
        var (ids, _) = await Pagina(a, b.Id, "");
        Assert.Equal(2, ids.Length);
        using var s2 = f.Services.CreateScope();
        Assert.Equal(2, s2.ServiceProvider.GetRequiredService<AppDbContext>().Mensagens
            .Count(m => m.RemetenteId == b.Id || m.DestinatarioId == b.Id));
    }

    [Fact]
    public async Task Limite_e_limitado_a_100()
    {
        var a = await Entrar("hist4a@t.com"); var b = await Entrar("hist4b@t.com");
        var (ids, _) = await Pagina(a, b.Id, "?limite=100000");
        Assert.Empty(ids);
    }
}
