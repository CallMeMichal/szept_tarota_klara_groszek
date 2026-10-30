# Logowanie bez haseł: kod z maila albo Google

Konto nie ma hasła. Klient loguje się jednorazowym, 6-cyfrowym kodem wysłanym na swój adres e-mail (ważnym 15 minut, wpisywanym na stronie) albo przez Google. Konto zakłada się samo przy pierwszym logowaniu, a samo zalogowanie kodem potwierdza adres, więc nie ma osobnej rejestracji, potwierdzania e-maila ani resetu hasła. Płacić można tylko po zalogowaniu: Zamówienie należy do Konta. (Zastąpione przez ADR 0004: można płacić bez logowania.) E-mail na potwierdzenie jest w formularzu płatności wpisany z góry adresem Konta, ale Klient może go zmienić (np. na adres, z którego korzysta częściej); Zamówienie i tak zostaje na jego Koncie. To zastępuje z pierwotnej specyfikacji (#1) logowanie e-mailem i hasłem i Konto zakładane przy zakupie. (ADR 0004 przywraca Konto zakładane przy zakupie, ale tylko na prośbę Klienta.)

Logowanie przez Google trafia do Konta o tym samym adresie e-mail co w Google, więc Klient ma jedno Konto niezależnie od sposobu logowania i nie łączy niczego ręcznie. Działa tylko, gdy Google potwierdził adres (`email_verified`); inaczej ktoś mógłby wejść na cudze Konto, zakładając konto Google z cudzym e-mailem. Nie zapisujemy identyfikatora konta Google: tożsamością Konta jest adres.

Sesja to ciasteczko HttpOnly, SameSite=Lax, ważne 30 dni (przedłużane przy używaniu). Bez ASP.NET Core Identity: bez haseł zostałaby z niej tylko tabela użytkowników, więc Konto to własna tabela, a logowanie to zwykłe uwierzytelnianie ciasteczkiem.

## Considered Options

- Link logowania w mailu (tak było na początku): link otwarty w przeglądarce aplikacji pocztowej albo na telefonie, gdy Koszyk jest na komputerze, logował nie tam, gdzie Klient czekał z Koszykiem. Kod wpisuje się w tej samej karcie. Krótki kod da się zgadywać, więc działa tylko najnowszy, przepada po 5 błędnych próbach, a na adres wychodzą najwyżej 3 kody na 15 minut.
- E-mail i hasło (ASP.NET Core Identity), jak w specyfikacji: Klient musiałby wymyślić i pamiętać hasło do sklepu, w którym kupuje rzadko, a my trzymać hasła, potwierdzanie adresu i reset hasła.
- Zakup bez logowania, z Kontem zakładanym po płatności: Klient podaje e-mail przy płatności, a Zamówienia widzi po zalogowaniu na ten adres. Odpadło na rzecz prostszej zasady: każde Zamówienie od początku należy do zalogowanego Konta.
