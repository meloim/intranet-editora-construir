# Intranet Editora Construir

API em `api/` (ASP.NET Core 8 + PostgreSQL).

- Rodar: `cd api/Intranet.Api && dotnet run` (http://localhost:5000, ambiente Development)
- Testes: `cd api && dotnet test`
- Dev: admin@intranet.local / Admin@123 (seed só em Development; exige PostgreSQL local, ver appsettings.Development.json)

## Web (Vue 3)

- Rodar: `cd web && npm install && npm run dev` (http://localhost:5173). Copie `.env.example` para `.env` se a API não estiver em localhost:5000.
- Testes: `npm test`; build: `npm run build`
