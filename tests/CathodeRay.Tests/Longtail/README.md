# Test dyrektyw long-tail

Trwały test `.out`/`.warning`/`.error`/`.assert`/`.define`/`.ifblank`/`.paramcount`:
`main.s` składany **naszym** asemblerem i porównywany z `expected.bin`
(test `LongtailTests.Golden_Matches_Reference`).

## Przypadki

| Katalog | Składnia | `expected.bin` z |
|---|---|---|
| `6502` | ca65 | ca65 + ld65 |

Więcej CPU nie ma sensu: to dyrektywy wspólne (niezależne od CPU), a pozostałe
referencje ich nie znają w tej formie.

## Pułapki zgodności (udokumentowane, nie bugi)

- Ten build ca65 (V2.18 Ubuntu) odrzuca wieloparametrowe `.macro` i funkcyjne
  `.define F(x)` — golden używa tylko konstrukcji akceptowanych przez
  referencję; reszta pokryta testami jednostkowymi według dokumentacji ca65.
- `.define` funkcyjne i wartości domyślne makr to rozszerzenia własne.
- Komunikaty (`.out`/`.warning`) zbierane tylko w przebiegu finalnym;
  przy niepowodzeniu asemblacji giną (błędy mają pierwszeństwo).

## Regeneracja

```
python3 tools/make_longtail_golden.py   # wymaga ca65, ld65
dotnet test --filter "FullyQualifiedName~LongtailTests"
```
