#!/usr/bin/env python3
"""Deasemblacja kodu naszego kompilatora i referencyjnego (cc65 dla 6502, SDCC dla Z80) na programach z samples/bench.

Użycie:
  python3 tools/disasm_compare.py [--cpu 6502|z80] [--out KATALOG] [--md PLIK] [--show] bench...   (np. max3 bubble)
  python3 tools/disasm_compare.py --cpu z80 --all --md docs/disasm-z80.md

Dla każdego benchu: kompiluje go naszym `cathode cc` i kompilatorem referencyjnym, wycina bajty samej funkcji ze zlinkowanego obrazu
(nasz) albo z obiektu (referencja), deasembluje (`da65` dla 6502, `z80dasm` dla Z80) i drukuje rozmiar, liczbę instrukcji oraz
wywoływane procedury wykonawcze. Wymaga po `dotnet build`: cc65+ca65+ld65+da65 (6502) albo sdcc+z80dasm (Z80).
Program benchu to plik z jedną funkcją (bez `main`); dopisujemy `int main(){return 0;}` tylko naszemu kompilatorowi.
"""
import argparse
import glob
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DLL = os.path.join(ROOT, "src/CathodeRay.Cli/bin/Debug/net10.0/cathode.dll")
BENCH = os.path.join(ROOT, "samples/bench")
PRELUDE = "typedef unsigned char uchar;\ntypedef unsigned int uint;\ntypedef unsigned long ulong;\n"
LOAD = 0x1000
CC65_CFG = """MEMORY { ZP: start=$0, size=$100, type=rw; RAM: start=$1000, size=$4000, type=rw, file=%O; }
SEGMENTS { ZEROPAGE: load=ZP, type=zp; CODE: load=RAM, type=ro; RODATA: load=RAM, type=ro, optional=yes;
           DATA: load=RAM, type=rw, optional=yes; BSS: load=RAM, type=rw, optional=yes; }
"""


def run(cmd, **kw):
    return subprocess.run(cmd, capture_output=True, text=True, **kw)


def need(*tools):
    missing = [t for t in tools if not shutil.which(t)]
    if missing:
        sys.exit("brak narzędzi: " + ", ".join(missing))


class Result:
    def __init__(self, name, size, dis, calls, note=""):
        self.name, self.size, self.dis, self.calls, self.note = name, size, dis, calls, note

    @property
    def instructions(self):
        return len(self.dis)


def clean_dis(text, cpu):
    """Zostawia tylko linie z instrukcjami (bez komentarzy, dyrektyw i pustych)."""
    lines = []
    for raw in text.splitlines():
        line = raw.split(";")[0].rstrip()
        if not line.strip():
            continue
        s = line.strip()
        if s.startswith((".", "org", "defb", "defw")) and not s.startswith(".byte"):
            if not s.startswith(".byte"):
                continue
        lines.append(line if re.match(r"^\S+:", line) else "    " + s)
    return lines


def count_calls(dis, cpu):
    pattern = r"\bjsr\s+(\S+)" if cpu == "6502" else r"\bcall\s+(\S+)"
    return sorted(c for c in set(re.findall(pattern, "\n".join(dis))) if not re.fullmatch(r"L[0-9A-F]{4}", c))


# ---------------------------------------------------------------- nasz kompilator

def ours(work, bench, cpu):
    src = os.path.join(work, "o_" + bench + ".c")
    with open(src, "w") as f:
        f.write(open(os.path.join(BENCH, bench + ".c")).read() + "\nint main() { return 0; }\n")
    binary, listing = os.path.join(work, bench + ".ours.bin"), os.path.join(work, bench + ".ours.lst")
    out = run(["dotnet", DLL, "cc", src, "--cpu", cpu, "-o", binary, "-l", listing])
    if out.returncode:
        return Result("nasz", 0, [], [], "błąd kompilacji: " + (out.stdout + out.stderr).strip()[:200])
    load = int(re.search(r"load \$([0-9A-Fa-f]+)", out.stdout).group(1), 16)
    labels = {}
    for line in open(listing):
        m = re.match(r"^([0-9A-F]{4})\s+(?:[0-9A-F]{2} ?)*\s*\S*o_%s\.c:\d+\s+(\w+):\s*$" % re.escape(bench), line)
        if m:
            labels[m.group(2)] = int(m.group(1), 16)
    if "main" not in labels:
        return Result("nasz", 0, [], [], "brak etykiety main w listingu")
    start = min(a for n, a in labels.items() if n != "main")
    end = labels["main"]
    image = open(binary, "rb").read()
    raw = os.path.join(work, bench + ".ours.raw")
    open(raw, "wb").write(image[start - load:end - load])
    return Result("nasz", end - start, disassemble(raw, start, cpu), [])


def disassemble(raw, origin, cpu):
    if cpu == "6502":
        text = run(["da65", "--cpu", "6502", "-S", hex(origin), raw]).stdout
    else:
        text = run(["z80dasm", "-a", "-g", hex(origin), raw]).stdout
    return clean_dis(text, cpu)


# ---------------------------------------------------------------- cc65 (6502)

def cc65(work, bench, opt):
    src = os.path.join(work, bench + ".c")
    with open(src, "w") as f:
        f.write(PRELUDE + open(os.path.join(BENCH, bench + ".c")).read())
    asm, obj = os.path.join(work, bench + ".cc65.s"), os.path.join(work, bench + ".cc65.o")
    if run(["cc65", opt, "-t", "none", "-o", asm, src]).returncode or run(["ca65", "-t", "none", "-o", obj, asm]).returncode:
        return Result("cc65", 0, [], [], "błąd kompilacji")
    m = re.search(r"CODE:\s+(\d+)", run(["od65", "--dump-segsize", obj]).stdout)
    function_size = int(m.group(1)) if m else 0
    names = re.findall(r"\.export\s+(\w+)", open(asm).read())
    with open(os.path.join(work, "c.cfg"), "w") as f:
        f.write(CC65_CFG)
    with open(os.path.join(work, "start.s"), "w") as f:
        f.write(".import %s\n.segment \"CODE\"\n_start: jsr %s\n        rts\n" % (names[0], names[0]))
    start_obj = os.path.join(work, "start.o")
    run(["ca65", "-t", "none", "-o", start_obj, os.path.join(work, "start.s")])
    linked = os.path.join(work, bench + ".cc65.bin")
    link = run(["ld65", "-C", os.path.join(work, "c.cfg"), "-o", linked, "-m", os.path.join(work, "cc65.map"), start_obj, obj, "none.lib"])
    if link.returncode:
        link = run(["ld65", "-C", os.path.join(work, "c.cfg"), "-o", linked, "-m", os.path.join(work, "cc65.map"), start_obj, obj,
                    "/usr/share/cc65/lib/none.lib"])
    if link.returncode:
        return Result("cc65", function_size, [], [], "błąd linkowania: " + link.stderr.strip()[:200])
    image = open(linked, "rb").read()
    raw = os.path.join(work, bench + ".cc65.raw")
    open(raw, "wb").write(image[4:4 + function_size])  # 4 B to starter (jsr + rts)
    dis = disassemble(raw, LOAD + 4, "6502")
    symbols = {}
    for name, address in re.findall(r"^(\w+)\s+([0-9A-F]{6})\s+RL?A", open(os.path.join(work, "cc65.map")).read(), re.M):
        symbols[int(address, 16)] = name
    for name, address in re.findall(r"\s(\w+)\s+([0-9A-F]{6})\s+RL?A", open(os.path.join(work, "cc65.map")).read()):
        symbols[int(address, 16)] = name
    named = []
    for line in dis:
        m = re.search(r"\b(jsr|jmp)\s+L([0-9A-F]{4})\b", line)
        if m and int(m.group(2), 16) in symbols:
            line = line.replace("L" + m.group(2), symbols[int(m.group(2), 16)])
        named.append(line)
    dis = named
    total = os.path.getsize(linked)
    note = "sama funkcja %d B; po zlinkowaniu z runtime cc65 %d B (w tym 4 B startera)" % (function_size, total)
    return Result("cc65", function_size, dis, count_calls(dis, "6502"), note)


# ---------------------------------------------------------------- SDCC (Z80)

def sdcc(work, bench, opt):
    src = os.path.join(work, bench + ".c")
    with open(src, "w") as f:
        f.write(PRELUDE + open(os.path.join(BENCH, bench + ".c")).read())
    rel = os.path.join(work, bench + ".sdcc.rel")
    if run(["sdcc", "-mz80", "-c", opt, "-o", rel, src]).returncode:
        return Result("sdcc", 0, [], [], "błąd kompilacji")
    size = 0
    externs = []
    chunks = {}
    for line in open(rel):
        parts = line.split()
        if line.startswith("A _CODE"):
            size = int(parts[3], 16)
        elif line.startswith("S ") and parts[-1].startswith("Ref"):
            externs.append(parts[1].lstrip("_"))
        elif line.startswith("T ") and len(parts) > 4:
            # XL3: adres 3 bajty (młodszy pierwszy), potem bajty kodu (w benchach jedynym obszarem z danymi jest _CODE)
            chunks[int(parts[1], 16) | (int(parts[2], 16) << 8)] = bytes(int(b, 16) for b in parts[4:])
    data = bytearray(size)
    for address, blob in chunks.items():
        data[address:address + len(blob)] = blob
    raw = os.path.join(work, bench + ".sdcc.raw")
    open(raw, "wb").write(data)
    dis = disassemble(raw, LOAD, "z80")
    note = "adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)"
    return Result("sdcc", len(data), dis, sorted(set(externs)), note)


# ---------------------------------------------------------------- raport

def report(bench, results, show):
    lines = ["## %s" % bench, "", "| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |", "| --- | ---: | ---: | --- |"]
    for r in results:
        lines.append("| %s | %d | %d | %s |" % (r.name, r.size, r.instructions, ", ".join(r.calls) or "-"))
    ours_size = next((r.size for r in results if r.name == "nasz"), 0)
    for r in results:
        if r.name != "nasz" and r.size and ours_size:
            lines.append("")
            lines.append("stosunek nasz / %s: %.2f" % (r.name, ours_size / r.size))
    for r in results:
        if r.note:
            lines.append("")
            lines.append("- %s: %s" % (r.name, r.note))
    if show:
        for r in results:
            lines += ["", "### %s" % r.name, "", "```asm"] + r.dis + ["```"]
    return "\n".join(lines) + "\n"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("bench", nargs="*")
    ap.add_argument("--cpu", choices=["6502", "z80"], default="6502")
    ap.add_argument("--all", action="store_true", help="wszystkie benche z samples/bench")
    ap.add_argument("--opt", default=None, help="opcje optymalizacji referencji (domyślnie -Osir dla cc65, --opt-code-size dla SDCC)")
    ap.add_argument("--out", help="katalog na pliki .dis")
    ap.add_argument("--md", help="zapisz raport Markdown do pliku (z kodem)")
    ap.add_argument("--show", action="store_true", help="drukuj deasemblację")
    args = ap.parse_args()
    if not os.path.exists(DLL):
        sys.exit("brak %s (dotnet build)" % DLL)
    benches = sorted(os.path.splitext(os.path.basename(p))[0] for p in glob.glob(os.path.join(BENCH, "*.c"))) if args.all else args.bench
    if not benches:
        ap.error("podaj benche albo --all")
    need(*(["cc65", "ca65", "ld65", "da65", "od65"] if args.cpu == "6502" else ["sdcc", "z80dasm"]))
    reference = cc65 if args.cpu == "6502" else sdcc
    opt = args.opt or ("-Osir" if args.cpu == "6502" else "--opt-code-size")
    sections = []
    with tempfile.TemporaryDirectory(prefix="cathode-disasm-") as work:
        for bench in benches:
            results = [ours(work, bench, args.cpu), reference(work, bench, opt)]
            if args.out:
                os.makedirs(args.out, exist_ok=True)
                for r in results:
                    with open(os.path.join(args.out, "%s.%s.%s.dis" % (bench, args.cpu, r.name)), "w") as f:
                        f.write("\n".join(r.dis) + "\n")
            sections.append(report(bench, results, args.show or bool(args.md)))
    text = "# Deasemblacja: nasz kompilator vs %s (%s)\n\n" % ("cc65" if args.cpu == "6502" else "SDCC", args.cpu) + "\n".join(sections)
    if args.md:
        with open(args.md, "w") as f:
            f.write(text)
    else:
        print(text)


if __name__ == "__main__":
    main()
