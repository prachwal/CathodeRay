---
description: Odśwież goldeny rozmiarów i sprawdź brak wzrostów
---

1. `UPDATE_TARGET_SIZES=1 dotnet test tests/CathodeRay.Tests/CathodeRay.Tests.csproj --filter "FullyQualifiedName~TargetSizeTests"`
2. `UPDATE_VREG_SIZES=1 dotnet test tests/CathodeRay.Tests/CathodeRay.Tests.csproj --filter "FullyQualifiedName~VRegSizeTests"`
3. `git diff tests/CathodeRay.Tests/target-sizes.txt tests/CathodeRay.Tests/vreg-sizes.txt`

Zestaw zmiany per (plik, cel): wypisz **każdy wzrost osobno** (wymaga świadomej decyzji i uzasadnienia) oraz łączny bilans spadków. Wzrost bez uzasadnienia → STOP i zgłoś, nie commituj.
