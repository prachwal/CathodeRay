#!/usr/bin/env python3
"""Porównanie rozmiaru kodu mini-C z cc65 i SDCC na małych funkcjach z samples/bench.

Użycie: python3 tools/compare.py [--write]   (bez --write tylko drukuje tabelę; z --write zapisuje docs/compare.md)
Wymaga zbudowanego CLI (dotnet build) oraz cc65/ca65/od65 i sdcc w PATH; brakujące narzędzia dają kolumnę n/a.
Rozmiar mini-C to segment CODE zlinkowanego programu z funkcją i pustym main minus ten sam pomiar samego pustego main;
funkcje pomocnicze mnożenia/dzielenia siedzą w module (u cc65/SDCC zostają w bibliotece i nie są liczone).
"""
import glob
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DLL = os.path.join(ROOT, "src/CathodeRay.Cli/bin/Debug/net10.0/cathode.dll")
PRELUDE = "typedef unsigned char uchar;\ntypedef unsigned int uint;\ntypedef unsigned long ulong;\n"
HELPERS = {"mul16", "div16", "shift"}


def run(cmd):
    return subprocess.run(cmd, capture_output=True, text=True)


def ours(work, body, cpu):
    src = os.path.join(work, "o.c")
    with open(src, "w") as f:
        f.write(body + "\nint main() { return 0; }\n")
    out = run(["dotnet", DLL, "cc", src, "--cpu", cpu, "-o", os.path.join(work, "o.bin"), "--stats"]).stdout
    m = re.search(r"CODE (\d+)", out)
    return int(m.group(1)) if m else None


def cc65(work, body, opt):
    if not shutil.which("cc65"):
        return None
    src = os.path.join(work, "x.c")
    with open(src, "w") as f:
        f.write(PRELUDE + body)
    asm, obj = os.path.join(work, "x.s"), os.path.join(work, "x.o")
    if run(["cc65", opt, "-t", "none", "-o", asm, src]).returncode or run(["ca65", "-t", "none", "-o", obj, asm]).returncode:
        return None
    m = re.search(r"CODE:\s+(\d+)", run(["od65", "--dump-segsize", obj]).stdout)
    return int(m.group(1)) if m else None


def sdcc(work, body, machine="-mz80"):
    if not shutil.which("sdcc"):
        return None
    src = os.path.join(work, "x.c")
    with open(src, "w") as f:
        f.write(PRELUDE + body)
    rel = os.path.join(work, "x.rel")
    if run(["sdcc", machine, "-c", "--opt-code-size", "-o", rel, src]).returncode:
        return None
    with open(rel) as f:
        return sum(int(m.group(1), 16) for m in re.finditer(r"A _CODE size ([0-9A-Fa-f]+)", f.read()))


def main():
    write = "--write" in sys.argv
    work = tempfile.mkdtemp(prefix="cathode-compare-")
    try:
        base = {cpu: ours(work, "", cpu) for cpu in ("6502", "z80")}
        rows = []
        for path in sorted(glob.glob(os.path.join(ROOT, "samples/bench/*.c"))):
            body = open(path).read()
            name = os.path.basename(path)[:-2]
            a, z = ours(work, body, "6502"), ours(work, body, "z80")
            rows.append((name, None if a is None else a - base["6502"], cc65(work, body, "-Os"), None if z is None else z - base["z80"], sdcc(work, body)))
        lines = ["| funkcja | mini-C 6502 | cc65 -Os | mini-C Z80 | SDCC Z80 |", "| --- | ---: | ---: | ---: | ---: |"]
        for r in rows:
            mark = "*" if r[0] in HELPERS else ""
            lines.append(f"| {r[0]}{mark} | " + " | ".join("n/a" if x is None else str(x) for x in r[1:]) + " |")

        def mean(mine, theirs):
            pairs = [(r[mine], r[theirs]) for r in rows if r[0] not in HELPERS and r[mine] and r[theirs]]
            return math.exp(sum(math.log(a / b) for a, b in pairs) / len(pairs)) if pairs else float("nan")

        lines += ["", f"Średnia geometryczna bez wierszy z `*`: mini-C / cc65 = {mean(1, 2):.2f}, mini-C / SDCC = {mean(3, 4):.2f}."]
        text = "\n".join(lines) + "\n"
        print(text)
        if write:
            with open(os.path.join(ROOT, "docs/compare.md"), "w") as f:
                f.write("# Rozmiar kodu: mini-C, cc65, SDCC\n\nSegment `CODE` w bajtach małych funkcji z `samples/bench` (jedna funkcja eksportowana na plik); wiersze z `*` mierzą\nmnożenie, dzielenie i przesunięcie o zmienną liczbę — u mini-C razem z pomocnikiem w module, u cc65 i SDCC bez biblioteki.\nOdświeżenie: `python3 tools/compare.py --write` (po `dotnet build`; wymaga cc65 i SDCC).\n\n" + text)
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
