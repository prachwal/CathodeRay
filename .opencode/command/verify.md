---
description: Build -warnaserror, potem testy celowane (filtr w argumencie)
---

Wykonaj po kolei:

1. `dotnet build -warnaserror --nologo`
   - Jeśli build NIE przejdzie: zatrzymaj się, pokaż błędy i NIE uruchamiaj testów.
2. Ustal filtr z `$ARGUMENTS`:
   - puste → pomiń testy i powiedz o tym wprost;
   - zawiera `~` lub `=` → użyj dosłownie (np. `FullyQualifiedName~TargetSizeTests`);
   - inaczej → `FullyQualifiedName~$ARGUMENTS`.
3. `dotnet test tests/CathodeRay.Tests/CathodeRay.Tests.csproj --nologo --filter "<FILTR>"`

Zaraportuj wynik liczbowo (Passed/Failed). Nie zmieniaj kodu ani plików — to wyłącznie weryfikacja.
