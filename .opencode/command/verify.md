---
description: Build z -warnaserror i testy celowane (nazwa klasy lub pełny filtr)
---

Uruchom po kolei i zaraportuj wynik liczbowo (Passed/Failed):

1. `dotnet build -warnaserror`
2. `dotnet test tests/CathodeRay.Tests/CathodeRay.Tests.csproj --filter "<FILTR>"`

Gdzie `<FILTR>`:
- jeśli `$ARGUMENTS` zawiera `~` lub `=`, użyj go dosłownie (np. `FullyQualifiedName~TargetSizeTests`);
- w przeciwnym razie opakuj: `FullyQualifiedName~$ARGUMENTS`.

Jeśli `$ARGUMENTS` jest puste, pomiń krok 2 i powiedz o tym wprost. Nie zmieniaj kodu ani plików — to wyłącznie weryfikacja.
