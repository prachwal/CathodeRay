#!/usr/bin/env python3
"""Generuje wzorce asemblera 6502 z oryginalnego ca65 (cc65).

Dla każdego CPU (6502, 6502x, 65c02) tworzy w tests/CathodeRay.Tests/Asm/<cpu>/:
  opcodes.s / opcodes.bin  - każda para mnemonik+tryb z danych ISA, którą ca65 przyjmuje, z binarką z ca65,
  rejected.txt             - linie, które ca65 odrzuca (nasz asembler też musi je odrzucić),
  <nazwa>.bin              - binarka z ca65 dla każdego ręcznie napisanego <nazwa>.s w katalogu.

oraz tests/CathodeRay.Tests/Asm/8080/opcodes.s/.bin z z80asm (z88dk, -m8080, mnemoniki Intel). Pominięte (sprawdza je
Intel8080AsmTests z bajtami z tabeli Intela): RST (z80asm oczekuje adresu RST 38h, Intel numeru wektora RST 7) oraz
JP/CP (z80asm czyta je jako Zilog: skok bezwarunkowy i porównanie, a u Intela to skok/wywołanie przy dodatnim wyniku).

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


def z80asm_8080(source: str) -> bytes:
    env = dict(os.environ, PATH="/opt/z88dk-2.4/bin:" + os.environ["PATH"], ZCCCFG="/opt/z88dk-2.4/lib/config")
    with tempfile.TemporaryDirectory() as tmp:
        t = pathlib.Path(tmp)
        (t / "p.asm").write_text(source)
        subprocess.run(["z80asm", "-m8080", "-b", "p.asm"], cwd=t, env=env, check=True, capture_output=True)
        return (t / "p.bin").read_bytes()


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


def main() -> None:
    golden_8080()
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
