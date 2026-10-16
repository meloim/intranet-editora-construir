namespace Intranet.Api.Dominio;

public class Mensagem
{
    public int Id { get; set; }
    public int RemetenteId { get; set; }
    public int DestinatarioId { get; set; }
    public string Texto { get; set; } = "";
    public DateTime EnviadaEm { get; set; } = DateTime.UtcNow;
    public DateTime? EditadaEm { get; set; }
    public DateTime? ApagadaEm { get; set; }
}
