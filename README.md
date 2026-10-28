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

Lista kont, kluczy i ustawień potrzebnych do uruchomienia strony na kontach Klary (zamiast deweloperskich): [docs/konfiguracja-produkcyjna.md](docs/konfiguracja-produkcyjna.md).

### Katalog Produktów

Produkty są treścią w repo: `backend/src/SzeptTarota.Api/Catalog/products.json`. Grafiki Produktów są tylko we frontendzie: `frontend/src/assets/products/<slug>.webp`; Produkt bez pliku dostaje księżyc marki. Backend wczytuje katalog przy starcie i nie wystartuje, jeśli jest błędny (np. powtórzony slug, Usługa albo Sesja bez długości). Slug jest adresem podstrony (`/oferta/<slug>`), więc raz opublikowanego nie zmieniamy. Ceny są w groszach. Startowa wersja to import oferty z naffy.io.

Usługa ma formularz (`form`): listę pól z kluczem (`camelCase`, np. `birthDate`), etykietą, typem (`text`, `longText`, `date`, `email`) i `required`. Klient wypełnia go przy dodawaniu Usługi do Koszyka; tę samą Usługę można dodać wiele razy, każdą z własnym formularzem, i poprawić formularz w Koszyku. Każda Usługa i Sesja ma długość Terminu w minutach (`durationMinutes`, na razie wszędzie 60). Serwer sprawdza formularze przy każdym sprawdzeniu Koszyka i przed płatnością, a odpowiedzi (z etykietami z chwili zakupu) zapisuje w Zamówieniu i pokazuje w mailu do Klary.

### Formularz kontaktowy

Formularz w sekcji Kontakt wysyła `POST /api/contact`, a backend przekazuje wiadomość mailem (SMTP, MailKit) na firmowy adres, z adresem piszącej osoby w Reply-To. Adres i serwer ustawia się w sekcji `Contact` (`RecipientEmail`, `Smtp:Host`, `Smtp:Port`, `Smtp:Username`, `Smtp:Password`, `Smtp:SenderEmail`); hasło trzymaj w user-secrets albo zmiennych środowiskowych (`Contact__Smtp__Password`), nie w repo. Połączenie z serwerem poczty musi być szyfrowane (port 465: TLS od razu, inne porty: STARTTLS), inaczej wysyłka się nie uda. Jeden adres IP może wysłać 5 wiadomości na 10 minut (`Contact:RateLimit:PermitLimit`, `Contact:RateLimit:WindowMinutes`); za reverse proxy trzeba włączyć forwarded headers, żeby limit liczył prawdziwe IP. Dopóki `RecipientEmail` albo `Smtp:Host` są puste, endpoint odpowiada 503, a formularz prosi o kontakt przez Instagram. Na GitHub Pages nie ma backendu, więc formularz tam zawsze pokazuje ten komunikat.

### Konto i logowanie

Konto nie ma hasła (ADR 0003). Na `/logowanie` Klient podaje e-mail, a `POST /api/account/login-code` wysyła na niego Kod logowania: 6 cyfr, jednorazowy, ważny 15 minut. Działa tylko najnowszy kod wysłany na adres, a po 5 błędnych próbach przestaje działać. W bazie leży tylko skrót kodu. Klient wpisuje kod na tej samej stronie, która wysyła adres i kod do `POST /api/account/login`; po zalogowaniu wraca na `?dalej=<ścieżka>` (np. do Koszyka), a bez niej do Konta. Przy pierwszym logowaniu zakłada się Konto na ten adres (małymi literami), a API ustawia ciasteczko sesji (`szept-tarota-konto`, HttpOnly, SameSite=Lax, 30 dni, przedłużane). `GET /api/account` zwraca adres zalogowanego Klienta, `GET /api/account/orders` jego opłacone Zamówienia od najnowszego (Moje Zamówienia, `/konto`), `POST /api/account/logout` wylogowuje. Endpointy wymagające logowania odpowiadają 401.

Jeden adres IP może poprosić o 5 kodów na 10 minut (`Account:LoginCodeRateLimit:PermitLimit`, `Account:LoginCodeRateLimit:WindowMinutes`), a na jeden adres e-mail wychodzą najwyżej 3 kody na 15 minut. Prób logowania z jednego IP może być 20 na 10 minut (`Account:LoginRateLimit:PermitLimit`, `Account:LoginRateLimit:WindowMinutes`), a każdy kod ma najwyżej 5 prób, liczonych atomowo przed sprawdzeniem kodu. Dopóki brakuje `Resend:ApiKey`, prośba o kod odpowiada 503. Lokalnie kod przychodzi więc tylko z kluczem Resend i tylko na adres właściciela konta Resend (domena testowa).

Logowanie przez Google: przycisk „Zaloguj przez Google” na `/logowanie` przechodzi na `GET /api/account/google?returnUrl=<ścieżka>`, API odsyła przeglądarkę do Google, a po powrocie na `/api/account/google/callback` loguje do Konta o adresie z Google (małymi literami; to samo Konto co przy logowaniu Kodem logowania, a przy pierwszym logowaniu nowe) i przekierowuje na `returnUrl` (tylko ścieżki tej strony, domyślnie `/konto`). Konto Google bez potwierdzonego adresu, anulowanie w Google i brak kluczy kończą się powrotem na `/logowanie?google=nieudane` (z `dalej`). Klucze są poza repo, jak inne sekrety: `Google:ClientId` i `Google:ClientSecret` (klient OAuth typu „Aplikacja internetowa” w Google Cloud Console); bez nich Google nie jest w ogóle rejestrowany. W kliencie OAuth trzeba dodać adres powrotu `<adres strony>/api/account/google/callback` (lokalnie `http://localhost:5173/api/account/google/callback`, przez proxy Vite). API buduje ten adres z `Shop:SiteUrl`, a nie z adresu zapytania, bo stoi za proxy (Vite, na hostingu proxy kończące TLS) i widziałoby swój wewnętrzny adres.

Klucze, którymi ASP.NET szyfruje ciasteczko sesji i refresh token Google Calendar (Data Protection), są domyślnie w profilu użytkownika serwera. Na hostingu ustaw `DataProtection:KeysDirectory` na trwały katalog poza bazą (np. wolumen), inaczej każdy restart wyloguje Klientów i zerwie połączenie z kalendarzem.

### Panel Klary i Google Calendar

Panel (`/panel`) widzi tylko Konto o adresie `Shop:KlaraEmail` (bez względu na wielkość liter). Każde Konto ma adres potwierdzony przy logowaniu (Kod logowania albo Google z `email_verified`), więc ktoś, kto poda adres Klary bez dostępu do jej skrzynki, nie wejdzie. Endpointy `/api/panel/*` odpowiadają 401 bez logowania i 403 dla innych Kont; `GET /api/account` zwraca `isKlara`, a Konto Klary ma przycisk do panelu.

Połączenie z kalendarzem (ADR 0002): przycisk „Połącz z Google Calendar” przechodzi na `GET /api/panel/calendar/connect`, API odsyła do zgody w Google (zakresy `calendar.calendarlist.readonly` i `calendar.events`, `access_type=offline`, zawsze z ekranem zgody, żeby Google wydał refresh token), a po powrocie na `/api/panel/calendar/callback` sprawdza `state` z ciasteczka, wymienia kod na refresh token i zapisuje go zaszyfrowany (Data Protection) w tabeli `calendar_connections`; potem wraca na `/panel?kalendarz=polaczono` albo `nieudane`. `GET /api/panel/calendar` pyta Google na żywo i zwraca stan: `notConnected`, `connected` (z listą kalendarzy) albo `broken` (cofnięta zgoda, wygasły token, utracone klucze Data Protection); wtedy Klara łączy się ponownie tym samym przyciskiem. `PUT /api/panel/calendar/session-calendar` ustawia kalendarz z blokami „Dostępna” (Kalendarz Sesji); musi to być kalendarz z jej konta Google inny niż główny, bo główny jest zawsze odczytywany jako zajętość. Wybór zostaje po ponownym połączeniu.

Konfiguracja aplikacji Google (ten sam klient OAuth co logowanie przez Google, `Google:ClientId` i `Google:ClientSecret`):

1. W Google Cloud Console włącz w projekcie **Google Calendar API**.
2. W kliencie OAuth dodaj adres powrotu `<adres strony>/api/panel/calendar/callback` (lokalnie `http://localhost:5173/api/panel/calendar/callback`).
3. Na ekranie zgody OAuth (Google Auth Platform → Data Access) dodaj zakresy `.../auth/calendar.calendarlist.readonly` i `.../auth/calendar.events`.
4. **Opublikuj aplikację: stan „In production”, nie „Testing”.** W trybie „Testing” Google unieważnia refresh token po 7 dniach i panel zacznie pokazywać „Połączenie nie działa”. Zakresy kalendarza są „wrażliwe”, więc publiczna aplikacja wymaga weryfikacji Google (strona główna, polityka prywatności, uzasadnienie zakresów, nagranie przebiegu zgody). Do czasu weryfikacji Klara zobaczy ostrzeżenie „Google nie zweryfikował tej aplikacji” i może przejść dalej przez „Zaawansowane”; limit 100 użytkowników niezweryfikowanej aplikacji nie przeszkadza, bo z kalendarzem łączy się tylko ona. Logowanie Klientów przez Google (zakresy `openid email profile`) weryfikacji nie wymaga.
5. Klara łączy się w panelu kontem Google, na którym ma Kalendarz Sesji i główny kalendarz.

### Terminy

Przy każdej Usłudze i Sesji Klient wybiera Termin w drugim kroku okienka „Dodaj do koszyka” (i zmienia go w Koszyku przez „Popraw dane”). `GET /api/products/<slug>/appointments?from=RRRR-MM-DD&to=RRRR-MM-DD` zwraca wolne Terminy zaczynające się w tych dniach czasu polskiego (zakres do 62 dni; kalendarz pobiera po miesiącu), z przesunięciem strefy Europe/Warsaw i długością Produktu. Dostępność ustala wyłącznie Klara: każde wydarzenie w Kalendarzu Sesji to blok „Dostępna” (stykające się bloki łączą się w jeden), a strona dzieli go od początku na kolejne Terminy o długości Produktu (blok 10:00–12:00 przy 60 minutach to 10:00 i 11:00). Odpadają Terminy, które nachodzą na zajęte wydarzenia z głównego kalendarza (te oznaczone w Google jako „Dostępny” i odrzucone zaproszenia nie zajmują czasu) albo na opłacone Terminy, oraz te, które już się zaczęły. Strona nie dokłada przerw ani granic: Termin może zacząć się dokładnie wtedy, gdy kończy się poprzedni.

Koszyk wysyła początek Terminu każdej pozycji (`appointmentStart`), a serwer przy każdym sprawdzeniu i przed płatnością odrzuca brak Terminu (`appointmentMissing`), Termin zajęty, miniony albo spoza bloków (`appointmentUnavailable`, także gdy dwie pozycje Koszyka chcą tego samego czasu) i Termin, którego nie da się teraz sprawdzić, bo kalendarz nie odpowiada (`appointmentUnverifiable`). Bez działającego połączenia z kalendarzem i wybranego Kalendarza Sesji API wolnych Terminów odpowiada 503. Termin trafia do Zamówienia i do maila do Klary; od opłacenia nie jest już wolny.

### Zakup i płatności (Stripe Checkout)

Płacić można tylko po zalogowaniu: niezalogowany Klient widzi w Koszyku zaproszenie do logowania i po wpisaniu kodu z maila wraca do Koszyka. Koszyk wysyła `POST /api/checkout` z pozycjami (po cenach jednej sztuki, które Klient widzi, z liczbą sztuk Ebooka od 1 do 99 i odpowiedziami z formularzy Usług; każda Usługa to osobna pozycja), e-mailem na potwierdzenie (w formularzu wpisany z góry adres Konta, do zmiany), imieniem i nazwiskiem Klienta i kluczami zaakceptowanych zgód (regulamin i polityka prywatności). Backend sprawdza wszystko ponownie: zmieniona cena albo błędna pozycja to 409 z aktualnym Koszykiem, błędy pól i brak wymaganych zgód to 400. Gdy wszystko się zgadza, tworzy Zamówienie na Konto Klienta, oczekujące na płatność (z podanym e-mailem, kopią nazw i cen, imieniem i nazwiskiem Klienta oraz zgodami z czasem akceptacji) i zwraca adres Stripe Checkout (BLIK, Przelewy24, karta). Po płatności Stripe odsyła Klienta na `/zamowienie/<id>` (strona z podziękowaniem, czeka na potwierdzenie; `GET /api/orders/<id>` widzi tylko Konto, które złożyło Zamówienie), a po przerwaniu na `/koszyk?platnosc=przerwana` z nietkniętym Koszykiem.

Stan Zamówienia zmienia wyłącznie podpisany webhook `POST /api/payments/stripe/webhook`: `checkout.session.completed` (opłacona) i `checkout.session.async_payment_succeeded` oznaczają Zamówienie jako opłacone i wysyłają maile (Klient: podsumowanie z Ebookami w załączniku; Klara: Zamówienie, Klient, formularze Usług i zgody), a `checkout.session.expired` i `checkout.session.async_payment_failed` jako wygasłe. Powtórzony webhook niczego nie robi drugi raz; gdy maile się nie wyślą, webhook odpowiada 500 i Stripe ponowi go później.

Konfiguracja (sekrety poza repo). Najprościej lokalnie: plik `backend/src/SzeptTarota.Api/appsettings.Local.json` w tym samym układzie sekcji co `appsettings.json`. Jest w `.gitignore`, wczytuje się tylko w Development (po `appsettings.Development.json`, nadpisuje też user-secrets) i nie trafia do `dotnet publish`. Można też użyć user-secrets albo zmiennych środowiskowych:

```sh
cd backend/src/SzeptTarota.Api
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."   # z `stripe listen`, patrz niżej
dotnet user-secrets set "Resend:ApiKey" "re_..."
dotnet user-secrets set "Shop:KlaraEmail" "adres@klary"
dotnet user-secrets set "Google:ClientId" "....apps.googleusercontent.com"
dotnet user-secrets set "Google:ClientSecret" "GOCSPX-..."
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

Ciasteczko sesji ma datę ważności liczoną z `FakeTimeProvider`, więc wg prawdziwego zegara bywa „przeterminowane”. Dlatego testy zalogowanego Klienta używają `api.CreateBrowser()` / `api.LoggedInClient(factory.Emails, email)` (`BrowserCookies` odsyła ciasteczka bez sprawdzania daty), a nie `CreateClient()`. Po przesunięciu zegara o ponad 30 dni sesja wygasa i trzeba zalogować się ponownie.

### Fake'i adapterów zewnętrznych

Każda zewnętrzna usługa (płatności, kalendarz, maile, pliki) jest za interfejsem z Application; czas to systemowy `TimeProvider`. Logowanie przez Google to handler ASP.NET, a nie interfejs z Application, więc `FakeGoogle` (`factory.Google`) podmienia tylko jego połączenie z serwerami Google (`BackchannelHttpHandler`); `client.LogInWithGoogle(factory.Google, email)` przechodzi całe przekierowanie jak przeglądarka. Obecnie: `FakeGoogleCalendar` (`factory.GoogleCalendar`; `GrantAccess()` daje kod, z którym Google odsyła po zgodzie, `RevokeAccess()` unieważnia wydane tokeny), `FakePaymentGateway` (`factory.Payments`, zapisuje sesje i buduje podpisane webhooki), `FakeEmailSender` (`factory.Emails`; `LoggedInClient` loguje Kodem logowania z jego maila), `FakeEbookStorage` i `FakeContactMailer`. W testach podmieniamy je zawsze tak samo (pierwszy przykład w `ApiFactory`: `TimeProvider` → `FakeTimeProvider`):

1. Fake w `tests/SzeptTarota.Api.Tests/Fakes/`, implementuje interfejs i trzyma stan w pamięci (np. lista wysłanych maili).
2. Publiczna właściwość w `ApiFactory`, np. `public FakeEmailSender Emails { get; } = new();`.
3. Rejestracja w `ConfigureTestServices` w `ApiFactory.ConfigureWebHost`: `services.ReplaceWithFake<IEmailSender>(Emails);`.
4. Test ustawia i sprawdza fake przez `factory.Emails`.

Fake'i są współdzielone w kolekcji, więc test ustawia stan, którego potrzebuje, zamiast liczyć na stan po poprzednim teście.
