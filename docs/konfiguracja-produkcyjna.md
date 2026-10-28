# Konfiguracja produkcyjna (konta i klucze Klary)

Lokalnie działamy na kontach i kluczach deweloperskich (`appsettings.Local.json`). Przed uruchomieniem strony dla Klary trzeba założyć lub przepiąć poniższe usługi na jej konta i ustawić ich klucze na serwerze. Sekretów nie wpisujemy do repo: na serwerze ustawiamy je jako zmienne środowiskowe (`Sekcja__Klucz`, np. `Stripe__SecretKey`) albo w pliku `appsettings.Production.json` poza repo.

## Ustawienia aplikacji

| Klucz | Co wpisać | Sekret |
| --- | --- | --- |
| `ConnectionStrings:Database` | Connection string do produkcyjnego PostgreSQL | tak |
| `Database:ApplyMigrationsOnStartup` | `true`, żeby backend sam stosował migracje przy starcie (albo stosujemy je ręcznie, patrz README) | nie |
| `Shop:SiteUrl` | Adres strony, np. `https://szepttarota.pl`, bez `/` na końcu. Z niego powstają adresy powrotu ze Stripe i z Google | nie |
| `Shop:KlaraEmail` | Adres Klary. Dostaje maile o Zamówieniach i **tylko Konto o dokładnie tym adresie ma dostęp do panelu** (`/panel`) | nie |
| `Stripe:SecretKey` | Klucz `sk_live_...` z konta Stripe Klary | tak |
| `Stripe:WebhookSecret` | `whsec_...` z webhooka założonego w Stripe (patrz niżej) | tak |
| `Resend:ApiKey` | Klucz `re_...` z konta Resend | tak |
| `Resend:From` | Nadawca z domeny Klary, np. `Szept Tarota <sklep@szepttarota.pl>` | nie |
| `Google:ClientId` | Identyfikator klienta OAuth z projektu Google Klary (logowanie przez Google i kalendarz) | nie |
| `Google:ClientSecret` | Sekret tego klienta (`GOCSPX-...`) | tak |
| `Contact:RecipientEmail` | Adres, na który przychodzą wiadomości z formularza kontaktowego | nie |
| `Contact:Smtp:Host`, `Port`, `Username`, `Password`, `SenderEmail` | Skrzynka SMTP do wysyłki formularza kontaktowego (port 465 albo 587 ze STARTTLS) | hasło tak |
| `EbookStorage:Directory` | Trwały katalog z plikami Ebooków na serwerze (ścieżki jak `ebookFileKey` w `products.json`) | nie |
| `DataProtection:KeysDirectory` | Trwały katalog (np. wolumen) **poza bazą** na klucze szyfrujące ciasteczka sesji i refresh token Google Calendar. Bez niego każdy restart wyloguje Klientów i zerwie połączenie z kalendarzem | nie, ale katalog musi być prywatny |

Limity zapytań (`Contact:RateLimit:*`, `Account:LoginCodeRateLimit:*`, `Account:LoginRateLimit:*`) mają rozsądne wartości domyślne i nie trzeba ich ustawiać.

## Stripe (płatności)

1. Konto Stripe na dane Klary, aktywowane do płatności na żywo, z włączonymi metodami BLIK, Przelewy24 i karty.
2. Developers → API keys: klucz `sk_live_...` → `Stripe:SecretKey`.
3. Developers → Webhooks → Add endpoint:
   - adres: `<Shop:SiteUrl>/api/payments/stripe/webhook`
   - zdarzenia: `checkout.session.completed`, `checkout.session.async_payment_succeeded`, `checkout.session.expired`, `checkout.session.async_payment_failed`
   - Signing secret `whsec_...` → `Stripe:WebhookSecret`.

## Resend (maile: kody logowania, potwierdzenia Zamówień)

1. Konto Resend Klary.
2. Domains → dodaj domenę Klary i ustaw u jej dostawcy domeny rekordy DNS, które pokaże Resend (SPF, DKIM). Dopóki domena nie jest zweryfikowana, maile dochodzą tylko na adres właściciela konta Resend.
3. API Keys → klucz z uprawnieniem wysyłki → `Resend:ApiKey`; nadawca z tej domeny → `Resend:From`.

## Google (logowanie przez Google i Google Calendar)

Jeden projekt w Google Cloud Console, najlepiej na koncie Google Klary, i jeden klient OAuth do obu funkcji.

1. Google Auth Platform → Branding: nazwa „Szept Tarota”, adres strony, link do polityki prywatności, e-mail kontaktowy, domena Klary w „Authorized domains”.
2. Clients → Create client → typ „Web application”. Identyfikator → `Google:ClientId`, sekret → `Google:ClientSecret`.
3. W tym kliencie, w „Authorized redirect URIs”, dodaj oba adresy powrotu:
   - `<Shop:SiteUrl>/api/account/google/callback` (logowanie przez Google)
   - `<Shop:SiteUrl>/api/panel/calendar/callback` (połączenie z kalendarzem)
4. APIs & Services → Library → włącz **Google Calendar API**.
5. Google Auth Platform → Data Access: dodaj zakresy `.../auth/calendar.calendarlist.readonly` i `.../auth/calendar.events` (obok domyślnych `openid`, `email`, `profile`).
6. Google Auth Platform → Audience: **Publish app, stan „In production”**. W trybie „Testing” Google unieważnia refresh token kalendarza po 7 dniach i panel pokaże „Połączenie nie działa”.
7. Zakresy kalendarza są wrażliwe, więc Google poprosi o weryfikację aplikacji (strona główna, polityka prywatności, uzasadnienie zakresów, nagranie przebiegu zgody). Do czasu weryfikacji Klara przy łączeniu kalendarza zobaczy ostrzeżenie „Google nie zweryfikował tej aplikacji” i przejdzie dalej przez „Zaawansowane”. Logowanie Klientów (`openid email profile`) weryfikacji nie wymaga.

## Po wdrożeniu (Klara)

1. Klara loguje się na stronie adresem z `Shop:KlaraEmail` (kodem z maila albo przez Google) i wchodzi w Konto → „Panel Klary”.
2. W Google Calendar zakłada osobny kalendarz (np. „Sesje”), w którym będzie wpisywać bloki „Dostępna”.
3. W panelu klika „Połącz z Google Calendar”, wybiera swoje konto Google, zaznacza wszystkie dostępy i wybiera ten kalendarz jako Kalendarz Sesji. Jej główny kalendarz strona odczytuje jako zajętość.
4. Panel powinien pokazać „Połączono”. Jeśli kiedyś pokaże „Połączenie nie działa”, wystarczy kliknąć „Połącz ponownie”.

## Za proxy (HTTPS)

Backend stoi za proxy frontendu albo hostingu kończącym TLS. Adresy powrotu z Google powstają z `Shop:SiteUrl`, więc działają bez dodatkowych ustawień. Żeby limity zapytań liczyły prawdziwe IP Klientów, trzeba jeszcze włączyć forwarded headers. Dziś aplikacja ich nie obsługuje, więc to do zrobienia przy wdrożeniu.
