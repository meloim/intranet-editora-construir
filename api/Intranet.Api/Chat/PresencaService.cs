using System.Collections.Concurrent;

namespace Intranet.Api.Chat;

// Quem está online = tem ao menos uma conexão SignalR aberta (várias abas contam como uma pessoa).
public class PresencaService
{
    private readonly ConcurrentDictionary<int, int> _conexoes = new();

    public bool EstaOnline(int usuarioId) => _conexoes.TryGetValue(usuarioId, out var n) && n > 0;

    // true se a pessoa acabou de ficar online
    public bool Conectou(int usuarioId) => _conexoes.AddOrUpdate(usuarioId, 1, (_, n) => n + 1) == 1;

    // true se a pessoa acabou de ficar offline
    public bool Desconectou(int usuarioId)
    {
        var n = _conexoes.AddOrUpdate(usuarioId, 0, (_, v) => Math.Max(0, v - 1));
        return n == 0;
    }
}
