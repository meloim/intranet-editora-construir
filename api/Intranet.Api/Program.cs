using Intranet.Api.Auth;
using Intranet.Api.Chat;
using Intranet.Api.Dados;
using Intranet.Api.Dominio;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var emMemoria = builder.Configuration.GetValue<bool>("UsarBancoEmMemoria");
if (emMemoria)
{
    var nomeBanco = "intranet-" + Guid.NewGuid();
    builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(nomeBanco));
}
else
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseNpgsql(builder.Configuration.GetConnectionString("Padrao")));

builder.Services.AddSingleton<TokenService>();
builder.Services.AddControllers();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = "intranet",
            ValidAudience = "intranet",
            IssuerSigningKey = TokenService.Chave(builder.Configuration),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        // SignalR (WebSocket) manda o token na query string
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddSignalR();
builder.Services.AddScoped<ChatService>();
builder.Services.AddSingleton<PresencaService>();
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origens").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("X-Tem-Mais")));

var app = builder.Build();

if (app.Environment.IsDevelopment() || emMemoria)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (app.Environment.IsDevelopment() && !emMemoria && !db.Usuarios.Any())
    {
        db.Usuarios.Add(new Usuario
        {
            Nome = "Administrador", Email = "admin@intranet.local", Admin = true, Setor = "TI",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
        });
        db.SaveChanges();
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.Run();

public partial class Program;
