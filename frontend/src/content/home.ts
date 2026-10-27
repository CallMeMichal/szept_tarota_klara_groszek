// Treści strony głównej.
//
// TREŚCI TYMCZASOWE: wszystko, co ma `placeholder: true`, to tekst roboczy do
// podmiany na teksty od Klary (spec #1, „Warunki uruchomienia”, pkt 5). Sekcja
// z takim polem pokazuje na stronie znacznik „Treść tymczasowa”. Po podmianie
// tekstu usuń `placeholder: true`, a znacznik zniknie.

export const instagram = {
  handle: 'szept_tarota',
  url: 'https://www.instagram.com/szept_tarota/',
}

export const hero = {
  placeholder: true,
  eyebrow: 'Klara Groszek · tarot, energia, intuicja',
  title: 'Szept Tarota',
  lead: 'Karty nie mówią głośno. Szepczą to, co już wiesz w głębi siebie. Pomogę Ci to usłyszeć: przez tarot, analizy, rytuały i Sesje online.',
  stats: [
    { value: '5000+', label: 'przeprowadzonych Sesji' },
    { value: '8,6 tys.', label: 'osób na Instagramie' },
  ],
}

export const about = {
  placeholder: true,
  title: 'O mnie',
  paragraphs: [
    'Mam na imię Klara. Od lat pracuję z tarotem, energią i intuicją, a przez ten czas przeprowadziłam ponad 5000 Sesji dla osób, które szukały odpowiedzi, spokoju albo nowego kierunku.',
    'Wierzę, że karty nie przepowiadają losu, tylko pokazują to, co dzieje się w nas i wokół nas. Moja praca to uważne słuchanie: Ciebie, kart i tego, co między słowami.',
    'Pracuję online, więc możemy spotkać się bez względu na to, gdzie jesteś.',
  ],
}

// Produkty w sekcji Oferta pochodzą z katalogu w API (backend/src/SzeptTarota.Api/Catalog).
export const offer = {
  title: 'Oferta',
  lead: 'Analizy, Rytuały, Sesje i Kursy, każde przygotowane indywidualnie dla Ciebie.',
}

export const testimonials = {
  placeholder: true,
  title: 'Opinie',
  items: [
    {
      quote:
        'Analiza od Klary trafiła w punkt. Dostałam odpowiedzi na pytania, których bałam się zadać na głos.',
      author: 'Anna',
    },
    {
      quote:
        'Sesja Reiki była dla mnie chwilą prawdziwego wyciszenia. Czułam się zaopiekowana od pierwszej minuty.',
      author: 'Marta',
    },
    {
      quote:
        'Klara mówi o kartach z ogromną wrażliwością. Wróciłam już trzeci raz i na pewno nie ostatni.',
      author: 'Kasia',
    },
  ],
}

export const faq = {
  placeholder: true,
  title: 'Najczęstsze pytania',
  items: [
    {
      question: 'Jak wygląda Sesja online?',
      answer:
        'Sesja odbywa się przez Google Meet w wybranym przez Ciebie Terminie. Link do niej dostaniesz mailem zaraz po zapłacie.',
    },
    {
      question: 'Kiedy dostanę Ebooka?',
      answer:
        'Od razu po zapłacie: Ebook przyjdzie w mailu i będzie czekał na Ciebie na Koncie.',
    },
    {
      question: 'Ile czeka się na Analizę lub Rytuał?',
      answer:
        'Każdą Usługę wykonuję indywidualnie. Czas realizacji podaję w opisie każdej z nich.',
    },
    {
      question: 'Czy muszę wierzyć w tarot, żeby skorzystać?',
      answer:
        'Nie. Wystarczy otwartość i pytanie, z którym przychodzisz. Resztą zajmiemy się razem.',
    },
  ],
}

export const contact = {
  placeholder: true,
  title: 'Kontakt',
  lead: 'Masz pytanie, zanim coś wybierzesz? Napisz do mnie na Instagramie, odpowiadam na każdą wiadomość.',
}
