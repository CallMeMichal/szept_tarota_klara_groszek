# Szept Tarota

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

Treści strony głównej są w `frontend/src/content/home.ts`.

### Katalog Produktów

Produkty są treścią w repo: `backend/src/SzeptTarota.Api/Catalog/products.json`. Grafiki Produktów są tylko we frontendzie: `frontend/src/assets/products/<slug>.webp`; Produkt bez pliku dostaje księżyc marki. Backend wczytuje katalog przy starcie i nie wystartuje, jeśli jest błędny (np. powtórzony slug, Sesja bez długości). Slug jest adresem podstrony (`/oferta/<slug>`), więc raz opublikowanego nie zmieniamy. Ceny są w groszach. Startowa wersja to import oferty z naffy.io.

Usługa ma formularz (`form`): listę pól z kluczem (`camelCase`, np. `birthDate`), etykietą, typem (`text`, `longText`, `date`, `email`) i `required`. Klient wypełnia go przy dodawaniu Usługi do Koszyka; tę samą Usługę można dodać wiele razy, każdą z własnym formularzem, i poprawić formularz w Koszyku. Serwer sprawdza formularze przy każdym sprawdzeniu Koszyka i przed płatnością, a odpowiedzi (z etykietami z chwili zakupu) zapisuje w Zamówieniu i pokazuje w mailu do Klary.

### Formularz kontaktowy

Formularz w sekcji Kontakt wysyła `POST /api/contact`, a backend przekazuje wiadomość mailem (SMTP, MailKit) na firmowy adres, z adresem piszącej osoby w Reply-To. Adres i serwer ustawia się w sekcji `Contact` (`RecipientEmail`, `Smtp:Host`, `Smtp:Port`, `Smtp:Username`, `Smtp:Password`, `Smtp:SenderEmail`); hasło trzymaj w user-secrets albo zmiennych środowiskowych (`Contact__Smtp__Password`), nie w repo. Połączenie z serwerem poczty musi być szyfrowane (port 465: TLS od razu, inne porty: STARTTLS), inaczej wysyłka się nie uda. Jeden adres IP może wysłać 5 wiadomości na 10 minut (`Contact:RateLimit:PermitLimit`, `Contact:RateLimit:WindowMinutes`); za reverse proxy trzeba włączyć forwarded headers, żeby limit liczył prawdziwe IP. Dopóki `RecipientEmail` albo `Smtp:Host` są puste, endpoint odpowiada 503, a formularz prosi o kontakt przez Instagram. Na GitHub Pages nie ma backendu, więc formularz tam zawsze pokazuje ten komunikat.

### Zakup i płatności (Stripe Checkout)

Koszyk wysyła `POST /api/checkout` z pozycjami (po cenach jednej sztuki, które Klient widzi, z liczbą sztuk Ebooka od 1 do 99 i odpowiedziami z formularzy Usług; każda Usługa to osobna pozycja), e-mailem, imieniem i nazwiskiem Klienta i kluczami zaakceptowanych zgód (regulamin i polityka prywatności, a przy Usłudze w Koszyku także żądanie rozpoczęcia jej przed upływem 14 dni). Backend sprawdza wszystko ponownie: zmieniona cena albo błędna pozycja to 409 z aktualnym Koszykiem, błędy pól i brak wymaganych zgód to 400. Gdy wszystko się zgadza, tworzy Zamówienie oczekujące na płatność (z kopią nazw i cen, imieniem i nazwiskiem Klienta oraz zgodami z czasem akceptacji) i zwraca adres Stripe Checkout (BLIK, Przelewy24, karta). Po płatności Stripe odsyła Klienta na `/zamowienie/<id>` (strona z podziękowaniem, czeka na potwierdzenie), a po przerwaniu na `/koszyk?platnosc=przerwana` z nietkniętym Koszykiem.

Stan Zamówienia zmienia wyłącznie podpisany webhook `POST /api/payments/stripe/webhook`: `checkout.session.completed` (opłacona) i `checkout.session.async_payment_succeeded` oznaczają Zamówienie jako opłacone i wysyłają maile (Klient: podsumowanie z Ebookami w załączniku; Klara: Zamówienie, Klient, formularze Usług i zgody), a `checkout.session.expired` i `checkout.session.async_payment_failed` jako wygasłe. Powtórzony webhook niczego nie robi drugi raz; gdy maile się nie wyślą, webhook odpowiada 500 i Stripe ponowi go później.

Konfiguracja (sekrety w user-secrets albo zmiennych środowiskowych, nie w repo):

```sh
cd backend/src/SzeptTarota.Api
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."   # z `stripe listen`, patrz niżej
dotnet user-secrets set "Resend:ApiKey" "re_..."
dotnet user-secrets set "Shop:KlaraEmail" "adres@klary"
```

- `Shop:SiteUrl`: adres frontendu, na który Stripe odsyła Klienta (w Development `http://localhost:5173`).
- `Resend:From`: nadawca maili; do czasu weryfikacji domeny Klary to domena testowa Resend (`onboarding@resend.dev`), która dostarcza tylko na adres właściciela konta Resend.
- `EbookStorage:Directory`: katalog z plikami Ebooków (domyślnie `backend/src/SzeptTarota.Api/App_Data`, poza repo). Klucz pliku z `products.json` (np. `ebooks/tarot-dla-poczatkujacych.pdf`) to ścieżka w tym katalogu.

Dopóki brakuje kluczy Stripe albo `Shop:SiteUrl` / `Shop:KlaraEmail`, rozpoczęcie płatności odpowiada 503.

#### Webhooki lokalnie przez Stripe CLI

Stripe nie dosięgnie `localhost`, więc lokalnie webhooki przekazuje [Stripe CLI](https://docs.stripe.com/stripe-cli):

```sh
stripe login
stripe listen --forward-to http://localhost:5292/api/payments/stripe/webhook
# wypisze "Your webhook signing secret is whsec_..." -> ustaw go jako Stripe:WebhookSecret i zrestartuj backend
```

Potem przejdź przez zakup na `http://localhost:5173` w trybie testowym Stripe (karta `4242 4242 4242 4242`, dowolna przyszła data i CVC; BLIK testowy: dowolny 6-cyfrowy kod). `stripe listen` pokazuje każde zdarzenie i odpowiedź backendu. Wygaśnięcie sesji można wywołać `stripe trigger checkout.session.expired`, ale takie zdarzenie dotyczy nowej sesji spoza sklepu, więc backend je potwierdzi i zignoruje; prawdziwe wygaśnięcie Zamówienia przychodzi 30 minut po porzuceniu płatności.

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

Każda zewnętrzna usługa (płatności, kalendarz, maile, pliki) jest za interfejsem z Application; czas to systemowy `TimeProvider`. Obecnie: `FakePaymentGateway` (`factory.Payments`, zapisuje sesje i buduje podpisane webhooki), `FakeEmailSender` (`factory.Emails`), `FakeEbookStorage` i `FakeContactMailer`. W testach podmieniamy je zawsze tak samo (pierwszy przykład w `ApiFactory`: `TimeProvider` → `FakeTimeProvider`):

1. Fake w `tests/SzeptTarota.Api.Tests/Fakes/`, implementuje interfejs i trzyma stan w pamięci (np. lista wysłanych maili).
2. Publiczna właściwość w `ApiFactory`, np. `public FakeEmailSender Emails { get; } = new();`.
3. Rejestracja w `ConfigureTestServices` w `ApiFactory.ConfigureWebHost`: `services.ReplaceWithFake<IEmailSender>(Emails);`.
4. Test ustawia i sprawdza fake przez `factory.Emails`.

Fake'i są współdzielone w kolekcji, więc test ustawia stan, którego potrzebuje, zamiast liczyć na stan po poprzednim teście.
