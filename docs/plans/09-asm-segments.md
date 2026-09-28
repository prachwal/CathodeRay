# Asembler: segmenty w jednym wywołaniu + --map (wariant A linkera) (status: zamknięty)

- [x] **1.** Składnia: .segment NAZWA + aliasy .code/.data/.bss, segment domyślny — Gotowe gdy: .segment "NAZWA" (cudzysłów jak .include) + .code/.data/.bss w ca65/mos, SEGMENT/CODE/DATA/BSS w intel/zilog; pierwszy segment domyślny CODE; zmiana segmentu w .if nieaktywnej gałęzi ignorowana; niezdefiniowany segment w użyciu = błąd
- [x] **2.** Rdzeń: liczniki per segment, symbole z segmentem, */$ per segment — Gotowe gdy: Pass trzyma licznik per segment (przełączanie zachowuje), symbole niosą segment (duplikat tylko w tym samym segmencie? decyzja: globalne jak dziś), */$ i _lineStart względem bieżącego segmentu, forward-ref między segmentami działa, .org ustawia licznik bieżącego segmentu
- [x] **3.** CLI: --map NAZWA@adres + składanie .bin/HEX z lukami — Gotowe gdy: cathode asm --map CODE@0600 --map DATA@2000 (powtarzalne, format NAZWA@adres hex/dec), segment bez adresu = błąd, nakładające się segmenty = błąd overlapping, .bin zeruje luki a HEX pomija puste obszary, listing grupuje po segmentach
- [x] **4.** Testy + golden Segments/ z ca65/z80asm + build/test/hygiene — Gotowe gdy: program CODE+DATA+BSS per CPU (6502/65c02/z80/8080) bajt w bajt z ca65+ld65 (.cfg) i z80asm (SECTION), golden Segments/ + generator make_segments_golden.py + build -warnaserror + test + hygiene OK

Postęp: 4/4 gotowych.
