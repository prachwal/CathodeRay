# Linker modułów (wariant B): obiekt, GLOBAL/EXTRN, cathode link (status: zamknięty)

- [x] **1.** Format obiektu: symbole, relokacje, odłożone wyrażenia, zapis/odczyt — Gotowe gdy: zdefiniowany format obiektu (JSON lub binarny): segmenty, symbole (globalne/lokalne), importy/eksporty, rekordy relokacji (offset, typ, symbol+dodatek), odłożone wyrażenia; zapis z asemblera i odczyt w linkerze, test round-trip
- [x] **2.** Asembler: GLOBAL/EXTRN, symbole zewnętrzne, relokacje, odroczone wyrażenia — Gotowe gdy: dyrektywy GLOBAL/EXTRN (aliasy per dialekt jak .global/.export), odwołanie do EXTRN nie jest błędem undefined, EmitField zostawia relokację zamiast liczby, wyrażenia z zewnętrznymi odkładane (nie liczone w pass 2), wybór formy przy nieznanym = najdłuższa
- [x] **3.** Linker: łączenie modułów, mapa pamięci, relokacja — Gotowe gdy: linker składa moduły (kolejność, duplikat globala = błąd, brak importu = błąd z modułem), liczy adresy segmentów z mapy, aplikuje relokacje (8/16-bit, PC-względne), wykrywa overflow relokacji
- [x] **4.** CLI: cathode link + .cfg + biblioteki — Gotowe gdy: cathode asm -o mod.o (obiekt zamiast binarki), cathode link mod.o... -m mapa.cfg -o out.bin (i -f hex), biblioteki (wiele .o / archiwum), format .cfg (MEMORY/SEGMENTS jak ld65 w uproszczeniu)
- [x] **5.** Testy wielomodułowe per CPU z referencjami + build/test/hygiene — Gotowe gdy: program z 2-3 modułów (wołania między modułami, BSS współdzielony) per CPU bajt w bajt z ca65+ld65 i z80asm, błędy linkera (duplikat, brak importu, overflow) + build -warnaserror + test + hygiene OK

Postęp: 5/5 gotowych.
