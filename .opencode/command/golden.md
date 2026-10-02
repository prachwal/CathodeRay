---
description: Odśwież goldeny rozmiarów i zaraportuj wzrosty
---

1. `UPDATE_TARGET_SIZES=1 dotnet test tests/CathodeRay.Tests/CathodeRay.Tests.csproj --nologo --filter "FullyQualifiedName~TargetSizeTests"`
2. `UPDATE_VREG_SIZES=1 dotnet test tests/CathodeRay.Tests/CathodeRay.Tests.csproj --nologo --filter "FullyQualifiedName~VRegSizeTests"`
3. `git diff tests/CathodeRay.Tests/target-sizes.txt tests/CathodeRay.Tests/vreg-sizes.txt`

Obie komendy nadpisują też render w `docs/target-sizes.md` / `docs/vreg-sizes.md` — uwzględnij go (sprawdź cały `git diff`).

Zestaw zmiany per (plik, cel): wypisz **każdy wzrost osobno** (wymaga uzasadnienia w nocie planu) oraz łączny bilans spadków.
Wzrost bez uzasadnienia → STOP, nie commituj.
