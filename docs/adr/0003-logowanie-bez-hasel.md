# Logowanie bez haseł: link z maila albo Google

Konto nie ma hasła. Klient loguje się jednorazowym linkiem wysłanym na swój adres e-mail (ważnym 15 minut) albo przez Google. Konto zakłada się samo przy pierwszym logowaniu, a samo zalogowanie linkiem potwierdza adres, więc nie ma osobnej rejestracji, potwierdzania e-maila ani resetu hasła. Płacić można tylko po zalogowaniu: Zamówienie należy do Konta. E-mail na potwierdzenie jest w formularzu płatności wpisany z góry adresem Konta, ale Klient może go zmienić (np. na adres, z którego korzysta częściej); Zamówienie i tak zostaje na jego Koncie. To zastępuje z pierwotnej specyfikacji (#1) logowanie e-mailem i hasłem i Konto zakładane przy zakupie.

Sesja to ciasteczko HttpOnly, SameSite=Lax, ważne 30 dni (przedłużane przy używaniu). Bez ASP.NET Core Identity: bez haseł zostałaby z niej tylko tabela użytkowników, więc Konto to własna tabela, a logowanie to zwykłe uwierzytelnianie ciasteczkiem.

## Considered Options

- E-mail i hasło (ASP.NET Core Identity), jak w specyfikacji: Klient musiałby wymyślić i pamiętać hasło do sklepu, w którym kupuje rzadko, a my trzymać hasła, potwierdzanie adresu i reset hasła.
- Zakup bez logowania, z Kontem zakładanym po płatności: Klient podaje e-mail przy płatności, a Zamówienia widzi po zalogowaniu na ten adres. Odpadło na rzecz prostszej zasady: każde Zamówienie od początku należy do zalogowanego Konta.
