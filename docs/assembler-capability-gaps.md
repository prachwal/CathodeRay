# Asemblery: różnice i braki CathodeRay wobec ca65 / z80asm / ASXXXX

Stan na: 2026-09-28. Porównanie celuje w odpowiedź „czego brakuje, żeby dogonić
klasyczne asemblery”, a nie w pełną dokumentację każdego narzędzia.

Kolumny: **CR** = `src/CathodeRay.Assembler`, **ca65** (6502), **z80asm**
(Z80, tryb `-m8080` dla 8080), **ASXXXX/as6800** (6800).

## 1. Tabela możliwości

| Cecha | CR | ca65 | z80asm (Z80 / 8080) | ASXXXX (6800) |
| --- | --- | --- | --- | --- |
| Wiele CPU w jednym wywołaniu | 6502/6502x/65C02/8080/z80/z80u/stub | 6502/65C02/65816… (`--cpu`) | Z80/R800/GBZ80/8080… (`-m`) | osobny binarny cel na CPU |
| Przebiegi | 2, odwołanie w przód = najdłuższa forma | wieloprzebiegowy + linker | wieloprzebiegowy + linker | 2 + linker (`aslink`) |
| Wybór formy jak oryginał | tak (zero page/absolutny, błąd „not available”) | tak (smart mode) | tak | tak (direct/extended) |
| Makra | ❌ | ✅ (`.macro`, `.define`) | ✅ (`MACRO`) | ✅ (`.macro`) |
| Asemblacja warunkowa | ✅ (`.if/.elseif/.else/.endif`, `IF/ELIF/ELSE/ENDIF`; operatory relacyjne) | ✅ | ✅ | ✅ |
| Include | ✅ (`.include`/`INCLUDE`, cykle, `--incdir`; bez `.incbin`) | ✅ | ✅ | ✅ |
| Moduły + linker | ❌ (jeden plik → `.bin`) | ✅ (`ld65`, segmenty) | ✅ (sekcje, biblioteki) | ✅ (obszary, `aslink`) |
| Listing | ✅ (`-l`, adres + bajty) | ✅ (+ mapa z linkera) | ✅ (`LSTON/LSTOFF`) | ✅ (`.list/.nlist`) |
| Symbole lokalne / zakresy | ❌ | ✅ (`.scope/.proc`, tanie etykiety) | ograniczone | ograniczone |
| Dyrektywy danych | `DB/DW/DS`, `ORG`, `END` (+ aliasy) | `.byte/.word/.res/.align` + ~100 innych | `DEFB/DEFW/DEFS` + `ORG` | `.db/.dw/.blkb` + `ORG` |
| Formaty wyjścia | surowy `.bin` | obiekt + `ld65` (bin/hex/…) | `.bin`/obiekt/biblioteka | Intel HEX / binary |
| Błędy | pierwszy błąd, z numerem linii | pełna lista z kontekstem | pełna lista | pełna lista |

## 2. Braki CR (uporządkowane po koszcie)

1. **Makra** — największa dziura funkcjonalna. Bez nich powtarzalne sekwencje
   (tablice wektorów, prologi) trzeba rozwijać ręcznie.
2. **`.incbin`** — include działa, wplatania binarek brak.
3. **Linker / segmenty** — jeden moduł, adresowanie tylko przez `ORG`.
   Powiązane: brak `.align`, `.res` poza `DS`, brak wyjścia HEX.
4. **Symbole lokalne i zakresy** — przy większych źródłach globalne etykiety
   kolizyjne (`loop` w dwóch procedurach).
5. **Raportowanie** — stop na pierwszym błędzie; oryginały zwracają listę.

## 3. Co CR robi dobrze (przewagi do utrzymania)

- **Wybór formy jak oryginał** (najdłuższa przy odwołaniu w przód, błąd
  niedostępnego trybu zamiast cichej reinterpretacji).
- **Weryfikacja złotem** z oryginalnych narzędzi (`tools/make_asm_golden.py`):
  `Asm/6502*`, `Asm/8080`, `Asm/z80*` + `rejected.txt`.
- **Dane zamiast kodu**: nowy CPU to JSON + moduł w `Isa/Targets` + wpis
  w `AssemblerTargets` (Z80: `Z80Set` + dialekt `Zilog`).
- **Wieloplikowość** (2026-09-28): `.include`/`INCLUDE` we wszystkich dialektach,
  ekspansja przed pass 1, cykle i `--incdir`, test `Link/` (9 przypadków, bajt
  w bajt z ca65/z80asm) — `Link/README.md`, generator `tools/make_link_golden.py`.
- **Asemblacja warunkowa** (2026-09-28): `.if/.elseif/.else/.endif` i
  `IF/ELIF/ELSE/ENDIF` (intel bez ELIF, jak ASM80), operatory relacyjne
  `= == != <> < <= > >=` w `Expression`, test `Cond/` (6 CPU bajt w bajt
  z ca65/z80asm) — `Cond/README.md`, generator `tools/make_cond_golden.py`.
- **Dialekty jako dane** (`SyntaxDialect`): ca65/MOS/Intel/Stub/Zilog.

## 4. Proponowana kolejność domknięć

1. Symbole lokalne (tanie etykiety) + `.incbin` — reszta wieloplikowości.
2. Makra bezparametryczne → z parametrami.
3. Linker/segmenty albo przynajmniej `.align` + Intel HEX (mniejszy krok).
4. Zbieranie wszystkich błędów zamiast stopu na pierwszym.
