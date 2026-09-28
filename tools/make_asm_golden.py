#!/usr/bin/env python3
"""Generuje wzorce asemblera 6502 z oryginalnego ca65 (cc65).

Dla każdego CPU (6502, 6502x, 65c02) tworzy w tests/CathodeRay.Tests/Asm/<cpu>/:
  opcodes.s / opcodes.bin  - każda para mnemonik+tryb z danych ISA, którą ca65 przyjmuje, z binarką z ca65,
  rejected.txt             - linie, które ca65 odrzuca (nasz asembler też musi je odrzucić),
  <nazwa>.bin              - binarka z ca65 dla każdego ręcznie napisanego <nazwa>.s w katalogu.

oraz tests/CathodeRay.Tests/Asm/8080/opcodes.s/.bin z z80asm (z88dk, -m8080, mnemoniki Intel). Pominięte (sprawdza je
Intel8080AsmTests z bajtami z tabeli Intela): RST (z80asm oczekuje adresu RST 38h, Intel numeru wektora RST 7) oraz
JP/CP (z80asm czyta je jako Zilog: skok bezwarunkowy i porównanie, a u Intela to skok/wywołanie przy dodatnim wyniku).

Z80: Asm/z80 (udokumentowane, z80asm -mz80_strict; rejected.txt = nieudokumentowane, które tryb ścisły odrzuca)
i Asm/z80u (wszystkie, z80asm -mz80). Linie z szablonów mcp_z80_instructions.json: n=12h, nn=1234h, e=$+5, d=+5.

Wymaga ca65 i ld65 w PATH oraz z88dk w /opt/z88dk-2.4. Uruchamiać z katalogu repo: python3 tools/make_asm_golden.py
"""
import os
import re
import json
import pathlib
import subprocess
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = ROOT / "tests" / "CathodeRay.Tests" / "Asm"
CPUS = {"6502": "6502", "6502x": "6502X", "65c02": "65C02"}
OPERANDS = {
    "Implied": "", "Accumulator": "a", "Immediate": "#$12", "ZeroPage": "$12", "ZeroPageX": "$12,x",
    "ZeroPageY": "$12,y", "Absolute": "$1234", "AbsoluteX": "$1234,x", "AbsoluteY": "$1234,y",
    "Indirect": "($1234)", "IndirectX": "($12,x)", "IndirectY": "($12),y", "ZeroPageIndirect": "($12)",
    "AbsoluteIndexedIndirect": "($1234,x)", "Relative": "*+5", "ZeroPageRelative": "$12,{self}",
}
# BBR/BBS: etykieta zamiast *, bo ca65 w drugim operandzie tych instrukcji podstawia pod * adres o 2 dalszy
# niż początek instrukcji (w pozostałych instrukcjach * = początek). Nasz asembler traktuje * jednolicie.
ALIASES = {"KIL": "JAM", "XAA": "ANE"}
CONFIG = "MEMORY { M: start=$0000, size=$10000, file=%O; }\nSEGMENTS { CODE: load=M, type=rw; }\n"


def ca65(cpu: str, source: str) -> bytes | None:
    with tempfile.TemporaryDirectory() as tmp:
        t = pathlib.Path(tmp)
        (t / "p.s").write_text(source)
        (t / "raw.cfg").write_text(CONFIG)
        ok = subprocess.run(["ca65", "--cpu", cpu, "p.s", "-o", "p.o"], cwd=t, capture_output=True).returncode == 0
        ok = ok and subprocess.run(["ld65", "-C", "raw.cfg", "p.o", "-o", "p.bin"], cwd=t, capture_output=True).returncode == 0
        return (t / "p.bin").read_bytes() if ok else None


def candidates() -> list[str]:
    lines = []
    for name in ("mcp_6502_instructions.json", "mcp_65c02_instructions.json"):
        for e in json.loads((ROOT / "data" / "instructions" / name).read_text())["instructions"]:
            mode = "AbsoluteIndexedIndirect" if e["opcode"] == "7C" and e["mnemonic"] == "JMP" else e["encoding"]
            line = f"{ALIASES.get(e['mnemonic'], e['mnemonic']).lower()} {OPERANDS[mode]}".rstrip()
            if "{self}" in line:
                label = "self_" + e["mnemonic"].lower()
                line = f"{label}: " + line.replace("{self}", label)
            if line not in lines:
                lines.append(line)
    return lines


def z80asm(cpu: str, source: str) -> bytes | None:
    env = dict(os.environ, PATH="/opt/z88dk-2.4/bin:" + os.environ["PATH"], ZCCCFG="/opt/z88dk-2.4/lib/config")
    with tempfile.TemporaryDirectory() as tmp:
        t = pathlib.Path(tmp)
        (t / "p.asm").write_text(source)
        ok = subprocess.run(["z80asm", f"-m{cpu}", "-b", "p.asm"], cwd=t, env=env, capture_output=True).returncode == 0
        return (t / "p.bin").read_bytes() if ok and (t / "p.bin").exists() else None


def z80asm_8080(source: str) -> bytes:
    binary = z80asm("8080", source)
    if binary is None:
        raise SystemExit("z80asm -m8080 rejects the generated 8080 source")
    return binary


def golden_z80() -> None:
    sample = re.compile(r"\b(nn|n|e)\b|\+d\)")
    values = {"nn": "1234h", "n": "12h", "e": "$+5", "+d)": "+5)"}
    entries = json.loads((ROOT / "data" / "instructions" / "mcp_z80_instructions.json").read_text())["instructions"]
    lines = [(sample.sub(lambda m: values[m.group(0)], e["mnemonic"]), e.get("variants") == "undocumented") for e in entries]
    documented = list(dict.fromkeys(line for line, undoc in lines if not undoc))
    undocumented = list(dict.fromkeys(line for line, undoc in lines if undoc and line not in documented))
    for folder, cpu, lines in (("z80", "z80_strict", documented), ("z80u", "z80", documented + undocumented)):
        out = OUT / folder
        out.mkdir(parents=True, exist_ok=True)
        source = f"; Wygenerowane przez tools/make_asm_golden.py z z80asm -m{cpu}\n\tORG 0600H\n" + "".join(f"\t{l}\n" for l in lines)
        binary = z80asm(cpu, source)
        if binary is None:
            raise SystemExit(f"z80asm -m{cpu} rejects Asm/{folder}/opcodes.s")
        (out / "opcodes.s").write_text(source)
        (out / "opcodes.bin").write_bytes(binary)
        print(f"{folder}: {len(lines)} instructions")
    for hand in sorted((OUT / "z80").glob("*.s")):
        if hand.name != "opcodes.s":
            binary = z80asm("z80_strict", hand.read_text())
            if binary is None:
                raise SystemExit(f"z80asm -mz80_strict rejects {hand}")
            hand.with_suffix(".bin").write_bytes(binary)
    rejected = [line for line in undocumented if z80asm("z80_strict", f"\t{line}\n") is None]
    (OUT / "z80" / "rejected.txt").write_text("\n".join(rejected) + "\n")
    print(f"z80: {len(rejected)} of {len(undocumented)} undocumented rejected by -mz80_strict")


def golden_8080() -> None:
    out = OUT / "8080"
    out.mkdir(parents=True, exist_ok=True)
    lines = []
    for e in json.loads((ROOT / "data" / "instructions" / "mcp_8080_instructions.json").read_text())["instructions"]:
        line = re.sub(r"\bd8\b", "12h", re.sub(r"\b(d16|a16)\b", "1234h", e["mnemonic"]))
        if line.split()[0] not in ("RST", "JP", "CP") and line not in lines:
            lines.append(line)
    source = "; Wygenerowane przez tools/make_asm_golden.py z z80asm -m8080\n\tORG 0600H\n" + "".join(f"\t{l}\n" for l in lines)
    (out / "opcodes.s").write_text(source)
    (out / "opcodes.bin").write_bytes(z80asm_8080(source))
    print(f"8080: {len(lines)} instructions")


def as6800(source: str) -> bytes | None:
    with tempfile.TemporaryDirectory() as tmp:
        t = pathlib.Path(tmp)
        (t / "p.s").write_text(source)
        ok = (
            subprocess.run(
                ["as6800", "-l", "p.lst", "-o", "p.o", "p.s"],
                cwd=t,
                capture_output=True,
            ).returncode
            == 0
        )
        if not ok:
            return None
        out = bytearray()
        for raw in (t / "p.lst").read_text().splitlines():
            match = re.match(r"^0 ([0-9A-F]{4}) : ((?:[0-9A-F]{2} ?)+)", raw)
            if match:
                out += bytes.fromhex(match.group(2))
        return bytes(out)


def golden_6800() -> None:
    out = OUT / "6800"
    out.mkdir(parents=True, exist_ok=True)
    lines = []
    branches = 0
    for e in json.loads(
        (ROOT / "data" / "instructions" / "mcp_6800_instructions.json").read_text()
    )["instructions"]:
        line = e["mnemonic"].lower().replace("#d16", "#$1234").replace("#d8", "#$12")
        line = re.sub(r"\bd8,x\b", "$12,x", line)
        line = re.sub(r"\bd8\b", "$12", line)
        line = re.sub(r"\ba16\b", "$1234", line)
        if line.endswith(" rel"):
            branches += 1
            line = line.replace(" rel", f" tgt{branches}\ntgt{branches}:")
        if line not in lines:
            lines.append(line)
    source = (
        "; Wygenerowane przez tools/make_asm_golden.py z as6800\n\torg $0600\n"
        + "".join(f"\t{l}\n" for l in lines)
    )
    binary = as6800(source)
    if binary is None:
        raise SystemExit("as6800 rejects Asm/6800/opcodes.s")
    (out / "opcodes.s").write_text(source)
    (out / "opcodes.bin").write_bytes(binary)
    print(f"6800: {len(lines)} instructions")
    rejected = ["ldaa $100,x", "jmp $100,x", "staa #$12"]
    for line in rejected:
        if as6800(f"\torg $0600\n\t{line}\n") is not None:
            raise SystemExit(f"as6800 accepts rejected line '{line}'")
    (out / "rejected.txt").write_text("\n".join(rejected) + "\n")
    for hand in sorted(out.glob("*.s")):
        if hand.name != "opcodes.s":
            binary = as6800(hand.read_text())
            if binary is None:
                raise SystemExit(f"as6800 rejects {hand}")
            hand.with_suffix(".bin").write_bytes(binary)




def main() -> None:
    golden_z80()
    golden_8080()
    golden_6800()
    lines = candidates()
    for folder, cpu in CPUS.items():
        out = OUT / folder
        out.mkdir(parents=True, exist_ok=True)
        accepted = [line for line in lines if ca65(cpu, f".org $0600\n{line}\n") is not None]
        rejected = [line for line in lines if line not in accepted]
        source = "; Wygenerowane przez tools/make_asm_golden.py z ca65 --cpu " + cpu + "\n.org $0600\n" + "\n".join(accepted) + "\n"
        (out / "opcodes.s").write_text(source)
        (out / "opcodes.bin").write_bytes(ca65(cpu, source))
        (out / "rejected.txt").write_text("\n".join(rejected) + "\n")
        for hand in sorted(out.glob("*.s")):
            if hand.name != "opcodes.s" and not hand.stem.endswith("_mos"):
                binary = ca65(cpu, hand.read_text())
                if binary is None:
                    raise SystemExit(f"ca65 --cpu {cpu} rejects {hand}")
                hand.with_suffix(".bin").write_bytes(binary)
        print(f"{folder}: {len(accepted)} accepted, {len(rejected)} rejected")


if __name__ == "__main__":
    main()
