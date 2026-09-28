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
| Makra | ✅ (`.macro`/`.endmacro`, `MACRO`/`ENDM`, parametry + domyślne, `.LOCAL`) | ✅ | ✅ | ✅ |
| Asemblacja warunkowa | ✅ (`.if/.elseif/.else/.endif`, `IF/ELIF/ELSE/ENDIF`; operatory relacyjne) | ✅ | ✅ | ✅ |
| Include | ✅ (`.include`/`INCLUDE`, cykle, `--incdir`, `.incbin`/`INCBIN`) | ✅ | ✅ | ✅ |
| Moduły + linker | ⚠️ segmenty w jednym wywołaniu (`.segment`, `--map`; bez linkera modułów) | ✅ (`ld65`, segmenty) | ✅ (sekcje, biblioteki) | ✅ (obszary, `aslink`) |
| Listing | ✅ (`-l`, adres + bajty) | ✅ (+ mapa z linkera) | ✅ (`LSTON/LSTOFF`) | ✅ (`.list/.nlist`) |
| Symbole lokalne / zakresy | ✅ (tanie `@`, zakres między globalami; bez `.scope`) | ✅ | ograniczone | ograniczone |
| Dyrektywy danych | `DB/DW/DS`, `ORG`, `END`, `.align`, `.incbin` (+ aliasy) | `.byte/.word/.res/.align` + ~100 innych | `DEFB/DEFW/DEFS` + `ORG` | `.db/.dw/.blkb` + `ORG` |
| Formaty wyjścia | `.bin` + Intel HEX (`-f hex`) | obiekt + `ld65` (bin/hex/…) | `.bin`/obiekt/biblioteka | Intel HEX / binary |
| Błędy | pełna lista (limit 20, `plik: line N`) | pełna lista z kontekstem | pełna lista | pełna lista |

## 2. Braki CR (uporządkowane po koszcie)

1. **Linker modułów** — segmenty w jednym wywołaniu działają (wariant A),
   ale brak półproduktu (obiektu), `GLOBAL`/`EXTRN` i komendy `link`
   (wariant B z `docs/linker-segments.md`).
2. **Zakresy symboli** (`.scope`/`.proc`) — tanie `@` działają, pełnych
   zakresów leksykalnych brak.
3. **Dyrektywy ca65 long-tail** (`~100` drobnych: `.out`, `.warning`,
   `.define` i in., w tym `.ifnblank`/`.paramcount` do makr).

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
- **Tanie etykiety i `.incbin`** (2026-09-28): `@` z zakresem między globalami
  (jak ca65/z80asm), `.incbin`/`INCBIN` jak `.include`, test `Locals/`
  (6 CPU bajt w bajt) — `Locals/README.md`, generator `tools/make_locals_golden.py`.
- **Makra** (2026-09-28): `.macro`/`.endmacro` i `MACRO`/`ENDM` z parametrami
  (w tym domyślnymi — rozszerzenie własne), `.LOCAL`/`LOCAL` → `__Mn_nazwa`,
  błędy z wywołaniem i definicją, test `Macros/` (6 CPU bajt w bajt) —
  `Macros/README.md`, generator `tools/make_macros_golden.py`.
- **`.align` i Intel HEX** (2026-09-28): `.align N[,fill]` (dowolne N≥1),
  `cathode asm -f hex`, test `AlignHexTests` (checksumy liczone ręcznie).
- **Wszystkie błędy** (2026-09-28): agregat `AssemblerError`, kontynuacja
  z synchronizacją PC, pass 2 tylko gdy pass 1 czysty, limit 20, CLI wypisuje
  `plik: line N` — test `ErrorsTests`, zero zmian w starych testach.
- **Segmenty** (2026-09-28): `.segment`/`.code`/`.data`/`.bss`
  (`SEGMENT`/`CODE`/`DATA`/`BSS`), liczniki per segment, BSS bez emisji,
  `cathode asm --map NAZWA@adres`, test `Segments/` (6 CPU bajt w bajt
  z ca65+ld65 i z80asm) — `Segments/README.md`, generator
  `tools/make_segments_golden.py`.
- **Dialekty jako dane** (`SyntaxDialect`): ca65/MOS/Intel/Stub/Zilog.

## 4. Proponowana kolejność domknięć

Wszystkie 4 punkty z 2026-09-28 zrobione (05–08) plus segmenty (09).
Zostały §2 powyżej (w tym wariant B linkera z `docs/linker-segments.md`).
