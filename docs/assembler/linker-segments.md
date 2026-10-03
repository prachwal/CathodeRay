# Moduły i linker: co robią referencje i czego brakuje w CathodeRay

Stan na: 2026-09-28. Ostatni duży brak z `docs/assembler-capability-gaps.md` §2.1.
Reszta braków (§2.2–§2.3) to dni pracy; linker to tygodnie — stąd osobny opis.

## 1. Co robią referencje

**ca65 + ld65** (rozdzielone celowo): ca65 nie zna adresów docelowych — składa
moduł do **obiektu** z nazwanymi segmentami (`.segment "CODE"`, `.code`,
`.data`, `.bss`, `.zeropage`, własne). Wyrażenia nierozwiązywalne w czasie
asemblacji (odwołania między segmentami) odkłada do obiektu. ld65 łączy
obiekty (i biblioteki `ar65`) według **konfiguracji pamięci** (plik `.cfg`:
`MEMORY` + `SEGMENTS` — który segment pod jaki adres, typ `ro`/`rw`/`bss`/`zp`,
wypełnienie). Linker liczy adresy, dokleja relokacje i pluje binarką/HEX-em
w dowolnym formacie. `.org` w ca65 istnieje, ale dokumentacja odradza —
adresowanie to robota linkera.

**z80asm (z88dk)**: model sekcji w jednym narzędziu (`SECTION name, ORG x` /
adresowanych przez linkera), moduły łączone w biblioteki i binarki tym samym
`z80asm` (osobny `z88dk-appmake` robi formaty docelowe: TAP, ROM, HEX…).
Sekcje mogą mieć przypisany adres albo płynąć za linkerem.

**ASXXXX/aslink**: obszary (`.area`, nazwa + atrybuty: `ABS`/`REL`, `OVR`/`CON`,
stronicowanie) + `aslink` z plikami sterującymi (`-b`, `-m` mapa, `-s` symbole).
Najstarszy model, najbliższy sprzętowi (banki ROM).

Wspólny mianownik: **asembler emituje relokowalny półprodukt** (nie binarkę),
a o adresach decyduje osobny krok z opisem pamięci.

## 2. Dlaczego tego nie ma w CathodeRay (analiza luki)

Nasz łańcuch dziś: `TwoPassAssembler` (2 przebiegi, symbole absolutne) →
surowy `.bin` / HEX od `Origin`. Konkretne braki składowe:

1. **Brak segmentów w języku**: `.org` ustawia jeden globalny licznik; nie ma
   `.segment`/`.code`/`.area`, więc nie da się opisać „kod tu, dane tam,
   BSS gdzie indziej” w jednym źródle.
2. **Brak półproduktu**: wynik to od razu bajty pod absolutnymi adresami.
   Nie ma formatu obiektu (symbole globalne/lokalne, importy/eksporty,
   rekordy relokacji, odłożone wyrażenia).
3. **Wyrażenia zawsze absolutne**: `FormSelector` i `EmitField` liczą na
   liczbach; odwołanie w przód = „najdłuższa forma”, nie „relokacja”.
   Linker wymagałby odraczania części wyrażeń poza pass 2.
4. **Jeden moduł**: `.include`/makra wklejają tekst (model ca65 sprzed
   linkera de facto też tak zaczynał); nie ma kompilacji rozdzielnej ani
   `GLOBAL`/`EXTRN`.
5. **Brak opisu pamięci**: CLI nie ma odpowiednika `ld65 -C config` ani
   `aslink -b`; jest tylko `--format bin|hex` z `Origin`.

## 3. Proponowany zakres (dwie prędkości)

**Wariant A — segmenty w jednym wywołaniu (tydzień, nie miesiące).**
Jeden plik (lub include'y) z kilkoma segmentami, adresy z nagłówka/CLI:
`.segment "CODE"` / `.segment "DATA"` + `cathode asm --map CODE@0600,DATA@2000`.
Bez formatu obiektu, bez linkowania modułów — każdy segment to osobny
przebieg liczników w tym samym `TwoPassAssembler`, wynik łączony do `.bin`/HEX.
Pokrywa 80% potrzeb (separacja kodu/danych/BSS, zero page) za ~20% kosztu.

**Wariant B — pełny linker (projekt).**
Format obiektu (JSON? prosty binarny), `GLOBAL`/`EXTRN`, relokacje
w `EmitField`, odroczone wyrażenia, komenda `cathode link` z plikiem mapy
pamięci, biblioteki. Dopiero gdy wariant A okaże się za ciasny (współdzielone
biblioteki między projektami, ROM-y bankowane).

Rekomendacja: **A teraz, B tylko na żądanie** — zgodnie z regułą mniejszego
kroku (jak `.align` + HEX zamiast pełnego linkera w planie 07).

## 4. Rozbicie wariantu A (szacunkowo, do planu)

1. Składnia: `.segment "NAZWA"` (+ aliasy `.code`/`.data`/`.bss`), segment
   domyślny, zmiana segmentu zachowuje liczniki per segment.
2. Rdzeń: liczniki per segment w `Pass`, symbole z segmentem, `*`/`$`
   względem bieżącego segmentu; przełączanie nie psuje forward-ref.
3. CLI: `--map` (nazwa@adres, powtarzalne), brak segmentu w mapie = błąd;
   `.bin`/HEX składane z segmentów po adresach (luki zerowane, jak dziś).
4. Testy: program z CODE+DATA+BSS na każdym CPU z referencją
   (ca65+ld65 z `.cfg`, z80asm z `SECTION`) bajt w bajt + golden `Segments/`.
