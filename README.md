# Szept Tarota — Klara Groszek

Aplikacja .NET 10 (ASP.NET Core Web API) + React 19 (Vite, TypeScript).

## Struktura

```
backend/
  SzeptTarota.slnx
  src/
    SzeptTarota.Api/             # endpointy HTTP, konfiguracja, Program.cs
    SzeptTarota.Application/     # przypadki użycia (zależy od Domain)
    SzeptTarota.Domain/          # model domeny, bez zależności
    SzeptTarota.Infrastructure/  # baza danych, zewnętrzne usługi (zależy od Application)
  tests/
    SzeptTarota.Api.Tests/       # testy integracyjne (xUnit + WebApplicationFactory)
frontend/                        # React + Vite, /api proxowane na backend
```

## Uruchomienie

```sh
# backend: http://localhost:5292
dotnet run --project backend/src/SzeptTarota.Api --launch-profile http

# frontend: http://localhost:5173
cd frontend
npm install
npm run dev
```

## Testy

```sh
dotnet test backend
cd frontend && npm run lint && npm run build
```
