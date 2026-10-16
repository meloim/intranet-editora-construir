using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace Intranet.Tests;

public class PresencaLidaTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private record Sessao(HttpClient Http, string Token, int Id);

    private async Task<Sessao> Entrar(string email)
    {
        f.CriarUsuario(email, "senha1234");
        var r = await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, senha = "senha1234" });
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        var token = json.GetProperty("token").GetString()!;
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Sessao(c, token, json.GetProperty("usuario").GetProperty("id").GetInt32());
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

    private static async Task<bool> Online(Sessao quem, int id)
    {
        var lista = await quem.Http.GetFromJsonAsync<JsonElement>("/api/contatos");
        return lista.EnumerateArray().First(e => e.GetProperty("id").GetInt32() == id).GetProperty("online").GetBoolean();
    }

    [Fact]
    public async Task Contatos_mostra_online_so_com_conexao_aberta_e_avisa_em_tempo_real()
    {
        var a = await Entrar("pl1a@t.com"); var b = await Entrar("pl1b@t.com");
        Assert.False(await Online(a, b.Id));

        var hubA = await ConectarHub(a);
        var eventos = new System.Collections.Concurrent.ConcurrentQueue<(int, bool)>();
        hubA.On<JsonElement>("presenca", p => eventos.Enqueue((p.GetProperty("usuarioId").GetInt32(), p.GetProperty("online").GetBoolean())));

        var hubB = await ConectarHub(b);
        await Esperar(() => eventos.Contains((b.Id, true)));
        Assert.True(await Online(a, b.Id));

        await hubB.StopAsync();
        await Esperar(() => eventos.Contains((b.Id, false)));
        Assert.False(await Online(a, b.Id));
        await hubA.StopAsync();
    }

    [Fact]
    public async Task Duas_abas_so_ficam_offline_quando_fecha_a_ultima()
    {
        var a = await Entrar("pl2a@t.com"); var b = await Entrar("pl2b@t.com");
        var h1 = await ConectarHub(b); var h2 = await ConectarHub(b);
        await h1.StopAsync();
        Assert.True(await Online(a, b.Id));
        await h2.StopAsync();
        await Esperar(async () => !await Online(a, b.Id));
    }

    [Fact]
    public async Task Mensagem_nasce_nao_lida_e_marcar_lida_avisa_o_remetente_em_tempo_real()
    {
        var a = await Entrar("pl3a@t.com"); var b = await Entrar("pl3b@t.com");
        var env = await a.Http.PostAsJsonAsync($"/api/conversas/{b.Id}/mensagens", new { texto = "oi" });
        var msg = await env.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(msg.GetProperty("lida").GetBoolean());
        var id = msg.GetProperty("id").GetInt32();

        var conv = await b.Http.GetFromJsonAsync<JsonElement>("/api/conversas");
        Assert.Equal(1, conv[0].GetProperty("naoLidas").GetInt32());

        var hubA = await ConectarHub(a);
        var recebidos = new System.Collections.Concurrent.ConcurrentQueue<int[]>();
        hubA.On<JsonElement>("mensagensLidas", p => recebidos.Enqueue(p.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32()).ToArray()));

        var r = await b.Http.PostAsync($"/api/conversas/{a.Id}/lida", null);
        r.EnsureSuccessStatusCode();
        await Esperar(() => recebidos.Any(x => x.Contains(id)));

        var hist = await a.Http.GetFromJsonAsync<JsonElement>($"/api/conversas/{b.Id}/mensagens");
        Assert.True(hist[0].GetProperty("lida").GetBoolean());
        conv = await b.Http.GetFromJsonAsync<JsonElement>("/api/conversas");
        Assert.Equal(0, conv[0].GetProperty("naoLidas").GetInt32());
        await hubA.StopAsync();
    }

    [Fact]
    public async Task Remetente_nao_marca_a_propria_mensagem_como_lida()
    {
        var a = await Entrar("pl4a@t.com"); var b = await Entrar("pl4b@t.com");
        await a.Http.PostAsJsonAsync($"/api/conversas/{b.Id}/mensagens", new { texto = "oi" });
        await a.Http.PostAsync($"/api/conversas/{b.Id}/lida", null);
        var hist = await a.Http.GetFromJsonAsync<JsonElement>($"/api/conversas/{b.Id}/mensagens");
        Assert.False(hist[0].GetProperty("lida").GetBoolean());
    }

    private static Task Esperar(Func<bool> cond) => Esperar(() => Task.FromResult(cond()));

    private static async Task Esperar(Func<Task<bool>> cond)
    {
        for (var i = 0; i < 50; i++) { if (await cond()) return; await Task.Delay(100); }
        Assert.True(await cond(), "condição não atendida a tempo");
    }
}
