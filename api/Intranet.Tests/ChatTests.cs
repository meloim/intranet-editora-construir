using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intranet.Api.Chat;
using Intranet.Api.Dados;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Intranet.Tests;

public class ChatTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private record Sessao(HttpClient Http, string Token, int Id);

    private async Task<Sessao> Entrar(string email, bool ativo = true)
    {
        f.CriarUsuario(email, "senha1234", ativo);
        var r = await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, senha = "senha1234" });
        if (!ativo) // desativado não loga: pega o id direto do banco
        {
            using var s = f.Services.CreateScope();
            var id = s.ServiceProvider.GetRequiredService<AppDbContext>().Usuarios.First(u => u.Email == email).Id;
            return new Sessao(f.CreateClient(), "", id);
        }
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        var token = json.GetProperty("token").GetString()!;
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Sessao(c, token, json.GetProperty("usuario").GetProperty("id").GetInt32());
    }

    private static Task<HttpResponseMessage> Enviar(Sessao de, Sessao para, string texto) =>
        de.Http.PostAsJsonAsync($"/api/conversas/{para.Id}/mensagens", new { texto });

    private void EnvelhecerMensagem(int id, int minutos)
    {
        using var s = f.Services.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Mensagens.Find(id)!.EnviadaEm = DateTime.UtcNow.AddMinutes(-minutos);
        db.SaveChanges();
    }

    private async Task<HubConnection> ConectarHub(Sessao s)
    {
        var hub = new HubConnectionBuilder().WithUrl("http://localhost/hubs/chat", o =>
        {
            o.HttpMessageHandlerFactory = _ => f.Server.CreateHandler();
            o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            o.AccessTokenProvider = () => Task.FromResult<string?>(s.Token);
        }).Build();
        await hub.StartAsync();
        return hub;
    }

    [Fact]
    public async Task Enviar_salva_e_aparece_no_historico_dos_dois()
    {
        var a = await Entrar("c1a@editora.com"); var b = await Entrar("c1b@editora.com");
        Assert.Equal(HttpStatusCode.Created, (await Enviar(a, b, "oi")).StatusCode);
        foreach (var (quem, outro) in new[] { (a, b), (b, a) })
        {
            var lista = await quem.Http.GetFromJsonAsync<JsonElement>($"/api/conversas/{outro.Id}/mensagens");
            Assert.Equal(1, lista.GetArrayLength());
            Assert.Equal("oi", lista[0].GetProperty("texto").GetString());
        }
    }

    [Fact]
    public async Task Terceiro_nao_ve_a_conversa()
    {
        var a = await Entrar("c2a@editora.com"); var b = await Entrar("c2b@editora.com"); var c = await Entrar("c2c@editora.com");
        await Enviar(a, b, "segredo");
        var lista = await c.Http.GetFromJsonAsync<JsonElement>($"/api/conversas/{a.Id}/mensagens");
        Assert.Equal(0, lista.GetArrayLength());
        var conv = await c.Http.GetFromJsonAsync<JsonElement>("/api/conversas");
        Assert.Equal(0, conv.GetArrayLength());
    }

    [Fact]
    public async Task Lista_de_conversas_traz_ultima_mensagem()
    {
        var a = await Entrar("c3a@editora.com"); var b = await Entrar("c3b@editora.com");
        await Enviar(a, b, "primeira"); await Enviar(b, a, "ultima");
        var conv = await a.Http.GetFromJsonAsync<JsonElement>("/api/conversas");
        Assert.Equal(1, conv.GetArrayLength());
        Assert.Equal(b.Id, conv[0].GetProperty("id").GetInt32());
        Assert.Equal("ultima", conv[0].GetProperty("ultimaMensagem").GetString());
    }

    [Fact]
    public async Task Validacoes_de_envio()
    {
        var a = await Entrar("c4a@editora.com"); var b = await Entrar("c4b@editora.com");
        var inativo = await Entrar("c4i@editora.com", ativo: false);
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(a, b, "  ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(a, a, "eu")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(a, inativo, "oi")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Http.PostAsJsonAsync("/api/conversas/9999/mensagens", new { texto = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/conversas")).StatusCode);
    }

    [Fact]
    public async Task Editar_propria_mensagem_dentro_de_15_minutos()
    {
        var a = await Entrar("c5a@editora.com"); var b = await Entrar("c5b@editora.com");
        var m = await (await Enviar(a, b, "erro")).Content.ReadFromJsonAsync<JsonElement>();
        var r = await a.Http.PutAsJsonAsync($"/api/mensagens/{m.GetProperty("id").GetInt32()}", new { texto = "certo" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var dto = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("certo", dto.GetProperty("texto").GetString());
        Assert.True(dto.GetProperty("editada").GetBoolean());
    }

    [Fact]
    public async Task Nao_edita_nem_apaga_mensagem_de_outro()
    {
        var a = await Entrar("c6a@editora.com"); var b = await Entrar("c6b@editora.com"); var c = await Entrar("c6c@editora.com");
        var id = (await (await Enviar(a, b, "minha")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Http.PutAsJsonAsync($"/api/mensagens/{id}", new { texto = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Http.DeleteAsync($"/api/mensagens/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Http.DeleteAsync($"/api/mensagens/{id}")).StatusCode);
    }

    [Fact]
    public async Task Apos_15_minutos_nao_edita_nem_apaga()
    {
        var a = await Entrar("c7a@editora.com"); var b = await Entrar("c7b@editora.com");
        var id = (await (await Enviar(a, b, "velha")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        EnvelhecerMensagem(id, 16);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Http.PutAsJsonAsync($"/api/mensagens/{id}", new { texto = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Http.DeleteAsync($"/api/mensagens/{id}")).StatusCode);
    }

    [Fact]
    public async Task Apagada_aparece_como_mensagem_apagada_para_os_dois()
    {
        var a = await Entrar("c8a@editora.com"); var b = await Entrar("c8b@editora.com");
        var id = (await (await Enviar(a, b, "oops")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await a.Http.DeleteAsync($"/api/mensagens/{id}")).StatusCode);
        var lista = await b.Http.GetFromJsonAsync<JsonElement>($"/api/conversas/{a.Id}/mensagens");
        Assert.Equal("mensagem apagada", lista[0].GetProperty("texto").GetString());
        Assert.True(lista[0].GetProperty("apagada").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Http.PutAsJsonAsync($"/api/mensagens/{id}", new { texto = "x" })).StatusCode);
    }

    [Fact]
    public async Task Hub_exige_autenticacao()
    {
        var r = await f.CreateClient().PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Destinatario_recebe_em_tempo_real_e_terceiro_nao()
    {
        var a = await Entrar("c9a@editora.com"); var b = await Entrar("c9b@editora.com"); var c = await Entrar("c9c@editora.com");
        var hubB = await ConectarHub(b); var hubC = await ConectarHub(c);
        var recebidaB = new TaskCompletionSource<JsonElement>();
        var recebidaC = new List<JsonElement>();
        hubB.On<JsonElement>("mensagemRecebida", m => recebidaB.TrySetResult(m));
        hubC.On<JsonElement>("mensagemRecebida", m => recebidaC.Add(m));

        await Enviar(a, b, "ao vivo");
        var got = await recebidaB.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("ao vivo", got.GetProperty("texto").GetString());
        await Task.Delay(300);
        Assert.Empty(recebidaC);
        await hubB.DisposeAsync(); await hubC.DisposeAsync();
    }

    [Fact]
    public async Task Envio_pelo_hub_salva_e_entrega()
    {
        var a = await Entrar("c10a@editora.com"); var b = await Entrar("c10b@editora.com");
        var hubA = await ConectarHub(a); var hubB = await ConectarHub(b);
        var recebida = new TaskCompletionSource<JsonElement>();
        hubB.On<JsonElement>("mensagemRecebida", m => recebida.TrySetResult(m));
        await hubA.InvokeAsync("EnviarMensagem", b.Id, "pelo hub");
        await recebida.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var lista = await b.Http.GetFromJsonAsync<JsonElement>($"/api/conversas/{a.Id}/mensagens");
        Assert.Equal("pelo hub", lista[0].GetProperty("texto").GetString());
        await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() => hubA.InvokeAsync("EnviarMensagem", b.Id, " "));
        await hubA.DisposeAsync(); await hubB.DisposeAsync();
    }
}
