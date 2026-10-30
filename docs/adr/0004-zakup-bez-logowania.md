# Zakup bez logowania, Konto na życzenie

Klient może zapłacić bez logowania: podaje e-mail w formularzu płatności, a potwierdzenie i Ebooki dostaje mailem. Formularz nie odsyła do logowania: kto kiedyś logował się na ten adres, i tak dostanie Zamówienie na swoje Konto. Zastępuje to zasadę z ADR 0003, że płacić można tylko po zalogowaniu, bo wymóg logowania przed zakupem to dodatkowy krok między Instagramem a płatnością, a wielu Klientów kupuje raz.

Zamówienie należy do Konta, gdy Klient płacił zalogowany (jego Konto, także gdy zmienił e-mail na potwierdzenie), gdy na adres z formularza istnieje już Konto, albo gdy niezalogowany Klient zaznaczył „Chcę założyć Konto”. Konto zakłada się od razu przy rozpoczęciu płatności, także gdy płatność potem nie dojdzie albo zostanie odrzucona: o Konto Klient poprosił, a nie o zakup; mail po opłaceniu mówi, że Konto powstało. W pozostałych przypadkach Zamówienie nie ma Konta i nie trafi do niego później, nawet gdy Klient założy Konto na ten sam adres: jego ślad to mail. Ebooki niezalogowany dostaje tylko w załączniku.

Bez logowania strona nie wie, kto wraca z przerwanej płatności, więc Termin zwalnia nie Konto, tylko powrót: przycisk „wróć” ze Stripe wygasza tamtą sesję płatności i od razu zwalnia jej Terminy. Strona po płatności pokazuje każdemu z linkiem samo podsumowanie, bez danych osobowych.

## Considered Options

- Zamówienie zawsze na Konto o adresie z formularza, zakładane bez pytania: Klient dostawałby Konto, o które nie prosił.
- Zamówienia bez Konta dołączane przy pierwszym logowaniu na ten adres: Klient, który nie chciał Konta, i tak by je miał z historią zakupów.
