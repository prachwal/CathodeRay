# Draft: przydział rejestrów dla Z80 (i ta sama konstrukcja dla 8080 i 6800)

Status: szkic projektowy, nic nie zaimplementowano. Podstawa: pomiar `tools/disasm_compare.py` (`docs/disasm-z80.md`): nasz kod jest od 1,8× do 9×
większy niż SDCC, a na `max3` 154 B kontra 53 B.

## 1. Problem

Cały potok zakłada model „komórki w pamięci + jeden akumulator”, który pasuje do 6502. Na Z80 kosztuje to podwójnie:

| operacja | dziś | z rejestrem |
| --- | --- | --- |
| odczyt bajtu komórki | `ld a,(nn)` 3 B, 13 T | `ld a,b` 1 B, 4 T |
| zapis bajtu komórki | `ld (nn),a` 3 B, 13 T | `ld b,a` 1 B, 4 T |
| argument ALU z komórki | `ld hl,nn` + `add a,(hl)` 4 B, 21 T | `add a,b` 1 B, 4 T |
| licznik `x++` (bajt) | `ld a,(nn)` `inc a` `ld (nn),a` 7 B | `inc b` 1 B |
| kopia 16 bitów | 4 instrukcje, 12 B | `ld d,b` `ld e,c` 2 B |

W `max3` ok. 40 z 56 instrukcji to przenoszenie bajtów między komórkami przez A.

## 2. Idea: rejestr jako „szybka lokalizacja” komórki

Dokładnie tak działa już przydział strony zerowej 6502 (`ZeroPageAllocator`, hak `ByteTarget.Tune`): komórka dostaje inną nazwę lokalizacji,
a `ByteSelector` niczego o tym nie wie, bo składa kod z `Octet` (tekst operandu). Dla rejestrów:

1. **`RegisterAllocator`** (nowy, obok `ZeroPageAllocator`, wołany z `Z80Target.Tune`) wybiera komórki i zapisuje mapę
   `symbol → rejestr(y)`: bajt do `B`, `C`, `D`, `E`; komórka 16-bitowa do pary `BC` albo `DE` (młodszy bajt `C`/`E`).
2. **`Z80Isa` staje się świadoma rejestrów**: `At(sym, offset)`/`Loc` zwracają nazwę rejestru zamiast adresu, a prymitywy rozpoznają
   operand-rejestr po nazwie (nazwy `B`,`C`,`D`,`E` są już w `Reserved`):
   `LoadA(b)` → `ld a,b`, `StoreA(b)` → `ld b,a`, `Alu(Add, b)` → `add a,b`, `Cmp(b)` → `cp b`, `TryStep(b)` → `inc b`/`dec b`,
   `PtrSetup(BC)` → `ld h,b` `ld l,c`.
3. **Komórki w rejestrach znikają z BSS** (jak przy ZP), więc dane też maleją.
4. `HL` zostaje zarezerwowane dla ISA (adres pośredni, `ld a,(hl)`, argumenty ALU z pamięci); `A` to akumulator; `IX`/`IY` dopiero w fazie 8.

Zmiana w `ByteSelector` (868 linii) jest zerowa albo minimalna, co jest największą zaletą: te same testy wyroczni sprawdzają obie wersje.

## 3. Które komórki wolno przenieść do rejestru

Warunki (wszystkie muszą być spełnione):

- komórka lokalna funkcji (przedrostek `funkcja__`), szerokość 1 albo 2 (4 dopiero po `WideLegalizer`, czyli jako dwie połówki 2-bajtowe);
- adres nieużyty (`AddrOf`), niebędąca `volatile` (zbiór `Module.Volatile` już istnieje);
- **żywa tylko między wywołaniami**: w przedziale od pierwszego odwołania do ostatniego nie ma `Ir.Call`. Przedział rozszerzamy na całą pętlę, jeśli odwołania są w
  pętli (wstecz skok `Jmp`/`BrCmp` do wcześniejszej etykiety). Wywołanie kończy życie rejestru, bo pierwsza wersja konwencji to „wszystkie rejestry są zmieniane
  przez wołanego”. To jednocześnie rozwiązuje rekurencję: rejestr nigdy nie żyje przez wołanie, więc ramka (`Saved`) go nie dotyczy;
- parametry `cc_arg*` i wynik `cc_ret*` zostają w pamięci (konwencja wspólna dla celów), ale kopia parametru do lokalnej może być w rejestrze
  (`ld a,(cc_arg1)` `ld b,a`).

Waga (jak dla ZP): liczba odwołań, w pętli ×8 na poziom, plus premia gdy komórka jest wskaźnikiem albo licznikiem `TryStep`. Rozwiązanie zachłanne po wadze
z konfliktami przedziałów (dwie komórki o rozłącznych przedziałach mogą dzielić rejestr). Mały budżet: 4 bajty (`B C D E`), a przy wywołaniach wszystko resetowane.

## 4. Kolejność prac (każdy krok zostawia testy zielone i tabelę rozmiarów nierosnącą)

| krok | opis | rozmiar |
| --- | --- | --- |
| 0 | harness: `tools/disasm_compare.py --cpu z80 --all` jako miara przed/po; flaga `--no-regs` do porównań | S |
| 1 | `Z80Isa`: rejestry jako operandy (`LoadA/StoreA/Alu/Cmp/TryStep`), test jednostkowy na ręcznym IR | M |
| 2 | `RegisterAllocator` bez liveness: tylko komórki 1-bajtowe w funkcjach bez wywołań (liście) | M |
| 3 | pary `BC`/`DE` dla komórek 2-bajtowych (`At` mapuje bajt 0 → `C`, bajt 1 → `B`) | M |
| 4 | liveness z pętlami, dzielenie rejestrów przez rozłączne przedziały | L |
| 5 | wywołania: rejestry żywe przez wołanie zapisywane `push bc`/`pop bc` wokół `call` (albo konwencja callee-saved dla `B`/`C`) | L |
| 6 | 16-bitowe prymitywy w `ByteIsa` (domyślnie rozbite na bajty): `Load16`, `Store16`, `Add16`, `Cmp16`; Z80 i 8080 nadpisują przez `ld hl,(nn)`, `add hl,de`, `inc hl` | L |
| 7 | port na 8080 (sekcja 5) | M |
| 8 | 6800: strona zerowa i akumulator B (sekcja 6) | M |
| 9 | `IX`/`IY` jako wskaźniki tablic i ramek (`ld a,(ix+d)`), argumenty i wynik w HL/DE dla funkcji niepublicznych | L |

Kroki 1-3 są najtańsze i przynoszą największy udział zysku, bo przenoszą liczniki pętli i zmienne pośrednie.

## 5. To samo dla 8080

Ten sam `RegisterAllocator` z inną listą rejestrów i innymi mnemonikami w `Intel8080Isa`:

| | Z80 | 8080 |
| --- | --- | --- |
| rejestry dla komórek | B C D E (pary BC, DE) | B C D E (pary BC, DE), bez IX/IY |
| `A ← r` | `ld a,b` | `mov a,b` |
| `r ← A` | `ld b,a` | `mov b,a` |
| ALU z rejestrem | `add a,b` | `add b` |
| porównanie | `cp b` | `cmp b` |
| krok o 1 | `inc b` / `dec b` | `inr b` / `dcr b` |
| 16-bit w pamięci | `ld hl,(nn)` / `ld (nn),hl` | `lhld nn` / `shld nn` |
| kopia pary | `ld d,b` `ld e,c` | `mov d,b` `mov e,c` |

Ograniczenie: 8080 nie ma `ld (nn),r` ani skoków względnych, więc oszczędność na zapisie 16-bitowym jest mniejsza niż na Z80, ale rejestry i tak dają
mniej instrukcji (`mov` to 1 B, `lda`/`sta` to 3 B). Kroki 1-6 piszemy raz z tablicą mnemonik w ISA, reszta jest wspólna.

## 6. To samo dla 6800

6800 nie ma rejestrów ogólnych, więc „szybka lokalizacja” to co innego:

- **Strona bezpośrednia** (`ldaa 5` 2 B zamiast `ldaa $1234` 3 B, o cykl szybciej) to dokładny odpowiednik strony zerowej 6502. Można użyć istniejącego
  `ZeroPageAllocator` bez zmian w algorytmie: potrzebne prefiks operandu (`z:`) i relokacja `Abs8` w asemblerze 6800 oraz przełączenie w `Tune` celu 6800.
- **Akumulator B** jako drugi rejestr: pomaga w łańcuchach 16-bitowych (jedna połówka w A, druga w B, mniej zapisów pośrednich). To osobny, mały krok
  w `M6800Isa`, dopiero po stronie bezpośredniej.
- Rejestr indeksowy `X` już służy jako wskaźnik (`PtrSetup`); przydział mógłby go wydzielić dla pętli po tablicach, jak `IndexFusion` dla 6502.

Wspólny mianownik: allocator wybiera komórki wg wagi, a cel deklaruje, jakie klasy lokalizacji ma (rejestr 8-bitowy, para, strona zerowa/bezpośrednia),
ile bajtów każda mieści i czy przeżywa wywołanie. Zalecany refaktor po kroku 3: wspólny `FastStorageAllocator` z konfiguracją per cel
(`ZeroPageAllocator` staje się jednym z jego trybów).

## 7. Oczekiwany zysk (szacunek, do zmierzenia w kroku 3)

`max3` na Z80: ok. 40 instrukcji przenoszenia bajtów po 3 B zmienia się w 1-2 B, więc 154 B → ok. 90-100 B po krokach 1-4 i ok. 75-85 B po kroku 6
(SDCC: 53 B). Prędkość rośnie bardziej niż rozmiar (4 T zamiast 13 T na dostęp). Te liczby są szacunkiem z liczby zamienianych instrukcji,
nie pomiarem; pierwszy pomiar da krok 2-3 na 14 benchach.

## 8. Ryzyka i sposób ich ograniczenia

- **Poprawność**: przedział życia rejestru błędnie liczony przy skokach → wynik zły tylko w niektórych ścieżkach. Ograniczenie: konserwatywnie (cała funkcja, jeśli
  jest jakikolwiek `Jmp`/`BrCmp` wstecz do etykiety przed pierwszym odwołaniem), testy `IrConformanceTests` i `TargetMatrixTests` na Z80 i 8080 bez zmian, plus
  nowy test generujący losowe małe programy (pętle, wywołania) i porównujący wynik z IR-owym interpreterem.
- **Runtime asemblerowy** (`stdlib/target/z80/rt_mul.s`, `rt_div.s`) zmienia rejestry: bezpieczne, bo żaden rejestr nie żyje przez `call` (krok 5 to zmienia świadomie).
- **Rozmiar nie może rosnąć**: `TargetSizeTests` (tabela w repo) jest bramką; zmiana idzie za flagą `--no-regs` do czasu zielonej tabeli.
- **Debugowanie**: komentarze `;c:` zostają, a do listingu dopisujemy, które komórki dostały rejestr (`; reg B = main__i`).

## 9. Otwarte pytania

1. Callee-saved `B`/`C` (mniej `push`/`pop` w pętlach z wywołaniami) czy pierwsza wersja bez tego?
2. Czy argumenty niepublicznych funkcji przekazywać w HL/DE (krok 9)? Zmienia konwencję opisaną w `docs/stub-calling-conv.md` i dotyka `Legalizer`.
3. `IXH`/`IXL` (nieudokumentowane, ale działają na każdym Z80 i emulatorze `Z80Cpu`?) jako dodatkowe 2 bajty rejestrów. Wymaga sprawdzenia emulatora.
