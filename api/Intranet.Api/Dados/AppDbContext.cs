using Intranet.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.Dados;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Mensagem> Mensagens => Set<Mensagem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Mensagem>().HasIndex(m => new { m.RemetenteId, m.DestinatarioId, m.EnviadaEm });
        b.Entity<Mensagem>().HasOne<Usuario>().WithMany().HasForeignKey(m => m.RemetenteId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Mensagem>().HasOne<Usuario>().WithMany().HasForeignKey(m => m.DestinatarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
