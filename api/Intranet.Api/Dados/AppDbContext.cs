using Intranet.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.Dados;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
    }
}
