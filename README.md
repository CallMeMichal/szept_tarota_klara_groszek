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

Wymagany Docker (PostgreSQL lokalnie i w testach).

```sh
# PostgreSQL 17 na localhost:5433 (dane w wolumenie postgres-data)
docker compose up -d

# backend: http://localhost:5292; w Development sam stosuje migracje przy starcie
dotnet run --project backend/src/SzeptTarota.Api --launch-profile http

# frontend: http://localhost:5173
cd frontend
npm install
npm run dev
```

Treści strony głównej są w `frontend/src/content/home.ts`. Teksty z `placeholder: true` są tymczasowe (na stronie mają znacznik „Treść tymczasowa”) i czekają na teksty od Klary.

### Katalog Produktów

Produkty są treścią w repo: `backend/src/SzeptTarota.Api/Catalog/products.json`. Grafiki Produktów są tylko we frontendzie: `frontend/src/assets/products/<slug>.webp`; Produkt bez pliku dostaje księżyc marki. Backend wczytuje katalog przy starcie i nie wystartuje, jeśli jest błędny (np. powtórzony slug, Sesja bez długości). Slug jest adresem podstrony (`/oferta/<slug>`), więc raz opublikowanego nie zmieniamy. Ceny są w groszach. Startowa wersja to import oferty z naffy.io.

Connection string jest w `ConnectionStrings:Database` (`appsettings.Development.json`). Poza Development migracje stosuje się ręcznie albo przez `Database:ApplyMigrationsOnStartup=true`.

## Podgląd na GitHub Pages

Workflow `.github/workflows/deploy-frontend-pages.yml` przy każdym pushu na `main` lintuje, buduje i wdraża frontend na GitHub Pages pod `/<repo>/` (`BASE_PATH`). Kopia `index.html` jako `404.html` sprawia, że wejście wprost na podstronę i odświeżenie działa. Pages to tylko podgląd testowy, bez backendu: przy `VITE_STATIC_CATALOG=true` build dokłada katalog Produktów z `products.json` jako statyczne pliki `api/products*.json`, więc Oferta i podstrony Produktów działają, ale nic nie da się kupić. https://callmemichal.github.io/szept_tarota_klara_groszek/

## Migracje (EF Core)

```sh
cd backend
dotnet tool restore
dotnet ef migrations add <Nazwa> --project src/SzeptTarota.Infrastructure --startup-project src/SzeptTarota.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/SzeptTarota.Infrastructure --startup-project src/SzeptTarota.Api
```

## Testy

```sh
dotnet test backend      # wymaga uruchomionego Dockera
cd frontend && npm run lint && npm run build
```

Testy integracyjne wysyłają zapytania HTTP do API uruchomionego przez `ApiFactory` (`WebApplicationFactory`). Fabryka startuje PostgreSQL w Testcontainers (jeden kontener na kolekcję `Api`) i stosuje migracje. Klasy testów oznaczamy `[Collection(ApiCollection.Name)]` i przyjmują `ApiFactory` w konstruktorze.

### Czas

Aplikacja bierze czas wyłącznie z wstrzykniętego `TimeProvider`, nigdy z `DateTime.Now` / `DateTimeOffset.UtcNow`. W testach to `FakeTimeProvider` dostępny jako `factory.Time` (`SetUtcNow`, `Advance`).

### Fake'i adapterów zewnętrznych

Każda zewnętrzna usługa (płatności, kalendarz, maile, pliki) jest za interfejsem z Application; czas to systemowy `TimeProvider`. W testach podmieniamy je zawsze tak samo (pierwszy przykład w `ApiFactory`: `TimeProvider` → `FakeTimeProvider`):

1. Fake w `tests/SzeptTarota.Api.Tests/Fakes/`, implementuje interfejs i trzyma stan w pamięci (np. lista wysłanych maili).
2. Publiczna właściwość w `ApiFactory`, np. `public FakeEmailSender Emails { get; } = new();`.
3. Rejestracja w `ConfigureTestServices` w `ApiFactory.ConfigureWebHost`: `services.ReplaceWithFake<IEmailSender>(Emails);`.
4. Test ustawia i sprawdza fake przez `factory.Emails`.

Fake'i są współdzielone w kolekcji, więc test ustawia stan, którego potrzebuje, zamiast liczyć na stan po poprzednim teście.
