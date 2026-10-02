---
description: Fuzz różnicowy vs gcc (funkcje liściowe + rekurencja)
---

1. `python3 tools/leaf_fuzz.py gen 60 /tmp/leaf-fuzz-batch`
2. `LEAF_FUZZ_DIR=/tmp/leaf-fuzz-batch dotnet test tests/CathodeRay.Tests/CathodeRay.Tests.csproj --nologo --filter "FullyQualifiedName~LeafFuzzTests"`
3. `python3 tools/recursion_fuzz.py gen 60 /tmp/rec-fuzz-batch`
4. `RECURSION_FUZZ_DIR=/tmp/rec-fuzz-batch dotnet test tests/CathodeRay.Tests/CathodeRay.Tests.csproj --nologo --filter "FullyQualifiedName~RecursionFuzzTests"`

Zaraportuj Passed/Failed obu filtrów; przy padzie wskaż wygenerowany plik i różnicę wyniku vs gcc. Wymaga `gcc`. Katalogi `gen` są nadpisywane przy każdym uruchomieniu. Nie zmieniaj źródeł.
