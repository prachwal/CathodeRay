#!/usr/bin/env python3
"""Import zestawów instrukcji z C# (PetEmulator, ../personal-004) do JSON MCP.

Źródło prawdy dla MCP `instructions` to data/instructions/mcp_<cpu>_instructions.json.
6502/65C02/6800: parsowane z C# personal-004. 8080: generowane z pełnej specyfikacji
(cpu-vibe-008/docs/8080/opcodes.md) — komplet 256 opcodów.

Użycie:
  python3 tools/import_isa_json.py            # wszystkie adaptery
  python3 tools/import_isa_json.py 6502 8080  # wybrane
  ISA_SOURCE_ROOT=/inny/personal-004/lib python3 tools/import_isa_json.py
"""

from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path
from typing import Match

REPO = Path(__file__).resolve().parent.parent
OUT_DIR = REPO / "data" / "instructions"
SOURCE = Path(os.environ.get("ISA_SOURCE_ROOT", REPO.parent / "personal-004" / "lib"))


def read(rel: str) -> str:
    path = SOURCE / rel
    if not path.is_file():
        raise FileNotFoundError(f"brak źródła: {path}")
    return path.read_text(encoding="utf-8")


def rx(pattern: str, text: str, flags: int = 0) -> Match[str]:
    """re.search z jawnym błędem zamiast None (usuwa Optional z typowania)."""
    match = re.search(pattern, text, flags)
    if match is None:
        raise ValueError(f"nie dopasowano wzorca: {pattern}")
    return match


def _groups() -> dict[str, str]:
    table = {
        "load": "LDA LDX LDY STA STX STY STZ TAX TAY TXA TYA TSX TXS LAX SAX LAS LXA XAA SHA SHX SHY TAS",
        "bits": "TSB TRB",
        "arithmetic": "ADC SBC INC DEC INX INY DEX DEY DCP ISC ISB SLO SRE RLA RRA ANC ALR ARR AXS USBC ANE",
        "logic": "AND ORA EOR",
        "shift": "ASL LSR ROL ROR",
        "compare": "CMP CPX CPY BIT",
        "branch": "BCC BCS BEQ BMI BNE BPL BVC BVS BRA",
        "jump": "JMP JSR RTS RTI",
        "stack": "PHA PHP PLA PLP PHX PHY PLX PLY",
        "flags": "CLC CLD CLI CLV SEC SED SEI",
        "system": "BRK NOP KIL HLT",
    }
    return {m: g for g, mset in table.items() for m in mset.split()}


GROUPS = _groups()


def group_of(mnemonic: str) -> str:
    return GROUPS.get(mnemonic.upper().split()[0], "undefined")


def write_json(name: str, description: str, notes: str, instructions: list[dict]) -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    dest = OUT_DIR / f"mcp_{name}_instructions.json"
    data = {
        "processor": name.upper(),
        "format_version": "1.0",
        "description": description,
        "notes": notes,
        "instructions": sorted(instructions, key=lambda i: int(i["opcode"], 16)),
    }
    dest.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"{name}: {len(instructions)} instrukcji -> {dest.relative_to(REPO)}")


def entry(opcode: int, mnemonic: str, cycles: int, size: int, group: str, sem: str,
          operands: list[dict] | None, encoding: str) -> dict:
    return {
        "opcode": f"{opcode:02X}",
        "mnemonic": mnemonic,
        "cycles": cycles,
        "words": size,
        "semantics": sem,
        "group": group,
        "encoding": encoding,
        "operands": operands,
        "variants": None,
    }


# --- 6502 / 65C02 -----------------------------------------------------------

MODE_6502 = {
    "I": ("Implied", 1), "A": ("Accumulator", 1), "M": ("Immediate", 2),
    "Z": ("ZeroPage", 2), "x": ("ZeroPageX", 2), "y": ("ZeroPageY", 2),
    "W": ("Absolute", 3), "X": ("AbsoluteX", 3), "Y": ("AbsoluteY", 3),
    "N": ("Indirect", 3), "i": ("IndirectX", 2), "j": ("IndirectY", 2),
    "r": ("Relative", 2),
}


def operands_6502(mode: str) -> list[dict] | None:
    return {
        "Implied": None,
        "Accumulator": None,
        "Immediate": [{"name": "d8", "type": "immediate8"}],
        "ZeroPage": [{"name": "zp", "type": "address8"}],
        "ZeroPageX": [{"name": "zp", "type": "address8"}, {"name": "X", "type": "index"}],
        "ZeroPageY": [{"name": "zp", "type": "address8"}, {"name": "Y", "type": "index"}],
        "Relative": [{"name": "rel", "type": "relative8"}],
        "Absolute": [{"name": "a16", "type": "address16"}],
        "AbsoluteX": [{"name": "a16", "type": "address16"}, {"name": "X", "type": "index"}],
        "AbsoluteY": [{"name": "a16", "type": "address16"}, {"name": "Y", "type": "index"}],
        "Indirect": [{"name": "a16", "type": "address16"}],
        "ZeroPageIndirect": [{"name": "(zp)", "type": "address8"}],
        "ZeroPageRelative": [{"name": "zp", "type": "address8"}, {"name": "rel", "type": "relative8"}],
        "IndirectX": [{"name": "(zp,X)", "type": "address8"}],
        "IndirectY": [{"name": "(zp),Y", "type": "address8"}],
    }[mode]


SEMANTICS_6502 = {
    "ADC": "A = A + M + C", "AND": "A = A & M", "ASL": "M = M << 1 (C = bit7)",
    "BCC": "skok gdy C=0", "BCS": "skok gdy C=1", "BEQ": "skok gdy Z=1",
    "BIT": "Z = (A & M)==0; N = bit7(M); V = bit6(M)", "BMI": "skok gdy N=1",
    "BNE": "skok gdy Z=0", "BPL": "skok gdy N=0",
    "BRK": "przerwanie SW; push PC+2, P; PC = [FFFE]",
    "BVC": "skok gdy V=0", "BVS": "skok gdy V=1",
    "CLC": "C = 0", "CLD": "D = 0", "CLI": "I = 0", "CLV": "V = 0",
    "CMP": "flagi z A - M", "CPX": "flagi z X - M", "CPY": "flagi z Y - M",
    "DEC": "M = M - 1", "DEX": "X = X - 1", "DEY": "Y = Y - 1",
    "EOR": "A = A ^ M", "INC": "M = M + 1", "INX": "X = X + 1", "INY": "Y = Y + 1",
    "JMP": "PC = operand", "JSR": "push (PC-1); PC = operand",
    "LDA": "A = M", "LDX": "X = M", "LDY": "Y = M",
    "LSR": "M = M >> 1 (C = bit0)", "NOP": "brak operacji (dummy read)",
    "ORA": "A = A | M", "PHA": "push A", "PHP": "push P", "PLA": "A = pop", "PLP": "P = pop",
    "ROL": "M = (M << 1) | C (C = bit7)", "ROR": "M = (M >> 1) | (C << 7) (C = bit0)",
    "RTI": "P = pop; PC = pop", "RTS": "PC = pop + 1",
    "SBC": "A = A - M - (1 - C)", "SEC": "C = 1", "SED": "D = 1", "SEI": "I = 1",
    "STA": "M = A", "STX": "M = X", "STY": "M = Y",
    "TAX": "X = A", "TAY": "Y = A", "TSX": "X = SP", "TXA": "A = X", "TXS": "SP = X", "TYA": "A = Y",
    "SLO": "M = ASL(M); A = A | M", "RLA": "M = ROL(M); A = A & M",
    "SRE": "M = LSR(M); A = A ^ M", "RRA": "M = ROR(M); A = A + M + C",
    "SAX": "M = A & X", "LAX": "A = X = M", "DCP": "M = M - 1; flagi z A - M",
    "ISC": "M = M + 1; A = A - M - (1 - C)", "ANC": "A = A & M; C = bit7",
    "ALR": "A = (A & M) >> 1", "ARR": "A = ((A & M) >> 1) | (C << 7)",
    "XAA": "A = (A | X) & M (niestabilny)", "AXS": "X = (A & X) - M",
    "LAS": "A = X = SP = M & SP", "TAS": "SP = A & X; M = SP & (H+1)",
    "SHX": "M = X & (H+1) (niestabilny)", "SHY": "M = Y & (H+1) (niestabilny)",
    "SHA": "M = A & X & (H+1) (niestabilny)", "KIL": "zatrzymanie CPU (JAM)",
    "USBC": "A = A - M - (1 - C) (nieoficjalny SBC #)",
    "TSB": "Z = ((A & M) == 0); M = M | A", "TRB": "Z = ((A & M) == 0); M = M & ~A",
    "STZ": "M = 0 (nie zmienia flag)", "WAI": "czekaj na przerwanie (RDY=1)",
    "STP": "zatrzymanie zegara (STP)",
}

# Cykle nieudokumentowanych opcodów: personal-004 defaultuje do 2 (błąd).
# Wartości bazowe wg kanonicznej tablicy NMOS 6502 (NESdev, 6502 all 256 opcodes).
CYCLES_6502: dict[int, int] = {}
for _base, _cyc in ((0x03, 8), (0x07, 5), (0x0F, 6), (0x13, 8), (0x17, 6), (0x1B, 7), (0x1F, 7)):
    for _step in (0x00, 0x20, 0x40, 0x60, 0xC0, 0xE0):
        CYCLES_6502[_base + _step] = _cyc
CYCLES_6502.update({
    0x83: 6, 0x87: 3, 0x8F: 4, 0x93: 6, 0x97: 4, 0x9B: 5, 0x9C: 5, 0x9E: 5, 0x9F: 5,
    0xA3: 6, 0xA7: 3, 0xAF: 4, 0xB3: 5, 0xB7: 4, 0xBB: 4, 0xBF: 4,
    0x02: 2, 0x12: 2, 0x22: 2, 0x32: 2, 0x42: 2, 0x52: 2, 0x62: 2, 0x72: 2,
    0x92: 2, 0xB2: 2, 0xD2: 2, 0xF2: 2,
})


MNEMONIC_6502 = {0x82: "NOP"}  # personal-004 błędnie KIL; kanonicznie NOP # (JAM to 12 opcodów bez 0x82)


def item_6502(opcode: int, mnemonic: str, cycles: int, length: int, mode: str) -> dict:
    mnemonic = MNEMONIC_6502.get(opcode, mnemonic)
    return entry(opcode, mnemonic, cycles, length, group_of(mnemonic),
                 SEMANTICS_6502.get(mnemonic, mode), operands_6502(mode), mode)


def parse_6502_core(apply_overrides: bool = True) -> dict[int, dict]:
    txt = read("PetEmulator.Cpu6502/Processor/OpcodeTables.cs")
    mnemonics = rx(r'const string Mnemonics = """(.*?)""";', txt, re.S).group(1).split()
    modes = [c for c in rx(r'const string Modes = """(.*?)""";', txt, re.S).group(1) if c.isalnum()]
    body = rx(r"BaseCyclesFor\(byte opcode\)\s*\{.*?return opcode switch\s*\{(.*?)\};", txt, re.S).group(1)
    cycles = {int(m.group(1), 16): int(m.group(2)) for m in re.finditer(r"0x([0-9A-Fa-f]{2})\s*=>\s*(\d+)", body)}
    assert len(mnemonics) == 256 and len(modes) == 256, (len(mnemonics), len(modes))
    table = {}
    for op in range(256):
        mode, length = MODE_6502[modes[op]]
        table[op] = item_6502(op, mnemonics[op], cycles.get(op, 2), length, mode)
    if apply_overrides:
        for op, cyc in CYCLES_6502.items():
            table[op]["cycles"] = cyc
    return table


def import_6502() -> None:
    table = parse_6502_core()
    write_json("6502", "MOS 6502 (NMOS) — pełne 256 opcodów (bazowe + nieudokumentowane)",
               "Zaimportowane z personal-004 PetEmulator.Cpu6502 (OpcodeTables.cs); cykle "
               "nieudokumentowanych opcodów poprawione wg NESdev (personal-004 defaultuje do 2).",
               list(table.values()))


def _patch(item: dict, group: str, sem: str) -> dict:
    item["group"], item["semantics"] = group, sem
    return item


def build_65c02() -> list[dict]:
    table = parse_6502_core(apply_overrides=False)
    txt = read("PetEmulator.Cpu6502/Processor/OpcodeTables.cs")
    cmos = txt[txt.index("CreateNativeCmos65C02CoreTable"):txt.index("CreateNativeR65C02SCoreTable")]
    for m in re.finditer(
        r'\(0x([0-9A-Fa-f]{2}),\s*"(\w+)",\s*AddressingMode\.(\w+),\s*(\d+),\s*(\d+),', cmos
    ):
        op = int(m.group(1), 16)
        table[op] = item_6502(op, m.group(2), int(m.group(5)), int(m.group(4)), m.group(3))
    nost = rx(
        r"foreach \(var opcode in new byte\[\]\s*\{(.*?)\}\)\s*\n\s*table\.Set\(CreateCoreDefinition\(opcode, \"NOP\", AddressingMode\.Implied, 1, 1",
        cmos, re.S,
    )
    for m in re.finditer(r"0x([0-9A-Fa-f]{2})", nost.group(1)):
        op = int(m.group(1), 16)
        table[op] = item_6502(op, "NOP", 1, 1, "Implied")
    for op in (0x02, 0x22, 0x42, 0x62):
        table[op] = item_6502(op, "NOP", 2, 2, "Immediate")
    table[0x5C] = item_6502(0x5C, "NOP", 8, 3, "AbsoluteX")
    # Warstwa R65C02S (Rockwell) — pominięta w poprzedniej wersji.
    for bit in range(8):
        table[0x07 + (bit << 4)] = _patch(item_6502(0x07 + (bit << 4), f"RMB{bit}", 5, 2, "ZeroPage"),
                                          "bits", f"M = M & ~(1 << {bit})")
        table[0x87 + (bit << 4)] = _patch(item_6502(0x87 + (bit << 4), f"SMB{bit}", 5, 2, "ZeroPage"),
                                          "bits", f"M = M | (1 << {bit})")
        table[0x0F + (bit << 4)] = _patch(item_6502(0x0F + (bit << 4), f"BBR{bit}", 5, 3, "ZeroPageRelative"),
                                          "branch", f"skok gdy bit {bit} M == 0")
        table[0x8F + (bit << 4)] = _patch(item_6502(0x8F + (bit << 4), f"BBS{bit}", 5, 3, "ZeroPageRelative"),
                                          "branch", f"skok gdy bit {bit} M == 1")
    table[0xCB] = _patch(item_6502(0xCB, "WAI", 3, 1, "Implied"), "system", "czekaj na przerwanie (RDY=1)")
    table[0xDB] = _patch(item_6502(0xDB, "STP", 3, 1, "Implied"), "system", "zatrzymanie zegara (STP)")
    # Poprawki wg W65C02S: RMW abs,X = 6 cykli (nie 7), BRK 2-bajtowy, BIT # tylko Z.
    for op in (0x1E, 0x3E, 0x5E, 0x7E, 0xDE, 0xFE):
        table[op]["cycles"] = 6
    table[0x00]["words"] = 2
    table[0x89]["semantics"] = "Z = (A & M) == 0 (N/V niezmienione; BIT #)"
    return [table[k] for k in sorted(table)]


def import_65c02() -> None:
    write_json(
        "65c02",
        "WDC W65C02S (CMOS) — pełna mapa 256 opcodów (NMOS + CMOS + Rockwell R65C02S)",
        "Zaimportowane z personal-004 PetEmulator.Cpu6502; warstwa R65C02S (RMB/SMB/BBR/BBS, WAI, STP) "
        "dodana; poprawki wg W65C02S: RMW abs,X = 6 cykli, BRK 2-bajtowy, BIT # tylko Z.",
        build_65c02(),
    )


# --- Intel 8080 -------------------------------------------------------------
# Generowane regułowo; źródło cykli/zachowań: cpu-vibe-008/docs/8080/opcodes.md
# (Intel 8080 — Complete Opcode Reference, all 256). MOV/ALU/branch są regularne.

REG8 = ["B", "C", "D", "E", "H", "L", "M", "A"]
CC = ["NZ", "Z", "NC", "C", "PO", "PE", "P", "M"]


def operands_8080(mnemonic: str) -> list[dict] | None:
    parts = mnemonic.split(None, 1)
    if len(parts) == 1:
        return None
    head, pair_ctx = parts[0], parts[0] in ("LXI", "DAD", "INX", "DCX", "PUSH", "POP")
    ops = []
    for raw in parts[1].split(","):
        name = raw.strip()
        if name == "d8":
            kind = "immediate8"
        elif name == "d16":
            kind = "immediate16"
        elif name == "a16":
            kind = "address16"
        elif name == "M":
            kind = "memory"
        elif name in ("BC", "DE", "HL", "SP", "PSW"):
            kind = "register pair"
        elif name.isdigit():
            kind = "restart vector"
        elif pair_ctx and name in ("B", "D", "H"):
            kind = "register pair"
        elif name in REG8:
            kind = "register"
        else:
            kind = "immediate"
        ops.append({"name": name, "type": kind})
    return ops


def _enc(op: int, size: int) -> str:
    return {1: f"{op:02X}", 2: f"{op:02X} d8", 3: f"{op:02X} d16 (LE)"}[size]


def _add(t: list[dict], op: int, mne: str, cyc: int, size: int, group: str, sem: str) -> None:
    t.append(entry(op, mne, cyc, size, group, sem, operands_8080(mne), _enc(op, size)))


def build_8080() -> list[dict]:
    t: list[dict] = []
    rot = ["RLC", "RRC", "RAL", "RAR", "DAA", "CMA", "STC", "CMC"]
    rot_sem = [
        "A=rotacja lewo; CY=stary bit7, bit0=stary bit7",
        "A=rotacja prawo; CY=stary bit0, bit7=stary bit0",
        "A=rotacja lewo przez CY; bit0=stary CY, CY=stary bit7",
        "A=rotacja prawo przez CY; bit7=stary CY, CY=stary bit0",
        "korekta dziesiętna A po ADD/ADC",
        "A=~A (bez flag)", "CY=1", "CY=~CY",
    ]
    # 0x00-0x3F
    for row in range(8):
        b = row * 8
        pair = ["BC", "DE", "HL", "SP"][row // 2]
        letter = ["B", "D", "H", "SP"][row // 2]
        _add(t, b, "NOP", 4, 1, "basic",
             "brak operacji" if row == 0 else "NOP (nieudokumentowany)")
        if row % 2 == 0:
            _add(t, b + 1, f"LXI {letter},d16", 10, 3, "load", f"{pair}=d16")
        else:
            _add(t, b + 1, f"DAD {letter}", 10, 1, "arithmetic", f"HL=HL+{pair}; tylko CY")
        col2 = [("STAX B", "[BC]=A", 7, 1), ("LDAX B", "A=[BC]", 7, 1),
                ("STAX D", "[DE]=A", 7, 1), ("LDAX D", "A=[DE]", 7, 1),
                ("SHLD a16", "[a16]=L,[a16+1]=H", 16, 3), ("LHLD a16", "L=[a16],H=[a16+1]", 16, 3),
                ("STA a16", "[a16]=A", 13, 3), ("LDA a16", "A=[a16]", 13, 3)][row]
        _add(t, b + 2, col2[0], col2[2], col2[3], "load", col2[1])
        if row % 2 == 0:
            _add(t, b + 3, f"INX {letter}", 5, 1, "arithmetic", f"{pair}={pair}+1")
        else:
            _add(t, b + 3, f"DCX {letter}", 5, 1, "arithmetic", f"{pair}={pair}-1")
        r = REG8[row]
        mem = r == "M"
        _add(t, b + 4, f"INR {r}", 10 if mem else 5, 1, "arithmetic", f"{r}={r}+1")
        _add(t, b + 5, f"DCR {r}", 10 if mem else 5, 1, "arithmetic", f"{r}={r}-1")
        _add(t, b + 6, f"MVI {r},d8", 10 if mem else 7, 2, "load", f"{r}=d8")
        _add(t, b + 7, rot[row], 4, 1, "accumulator", rot_sem[row])
    # 0x40-0x7F: MOV r,r (0x76 = HLT)
    for op in range(0x40, 0x80):
        if op == 0x76:
            _add(t, op, "HLT", 7, 1, "system", "zatrzymanie; wzbudzenie przerwaniem")
            continue
        dst, src = REG8[(op >> 3) & 7], REG8[op & 7]
        _add(t, op, f"MOV {dst},{src}", 7 if "M" in (dst, src) else 5, 1, "load", f"{dst}={src}")
    # 0x80-0xBF: ALU r
    alu = [("ADD", 0x80, "A=A+r"), ("ADC", 0x88, "A=A+r+CY"), ("SUB", 0x90, "A=A-r"),
           ("SBB", 0x98, "A=A-r-CY"), ("ANA", 0xA0, "A=A&r; CY=0"), ("XRA", 0xA8, "A=A^r; CY=0"),
           ("ORA", 0xB0, "A=A|r; CY=0"), ("CMP", 0xB8, "flagi A-r; A bez zmian")]
    for name, base, sem in alu:
        group = "logic" if name in ("ANA", "XRA", "ORA", "CMP") else "arithmetic"
        for i, r in enumerate(REG8):
            _add(t, base + i, f"{name} {r}", 7 if r == "M" else 4, 1, group, sem.replace("r", r))
    # 0xC0-0xFF: Rcc/Jcc/Ccc/RET/CALL/PUSH/POP/imm/RST + kontrolne
    imm = [("ADI", "A=A+d8", "arithmetic"), ("ACI", "A=A+d8+CY", "arithmetic"),
           ("SUI", "A=A-d8", "arithmetic"), ("SBI", "A=A-d8-CY", "arithmetic"),
           ("ANI", "A=A&d8; CY=0", "logic"), ("XRI", "A=A^d8; CY=0", "logic"),
           ("ORI", "A=A|d8; CY=0", "logic"), ("CPI", "flagi A-d8; A bez zmian", "logic")]
    for i in range(8):
        b = 0xC0 + i * 8
        _add(t, b, f"R{CC[i]}", 5, 1, "jump", f"powrót gdy {CC[i]}; cykle 5/11")
        if i % 2 == 0:
            pair = ["B", "D", "H", "PSW"][i // 2]
            _add(t, b + 1, f"POP {pair}", 10, 1, "load", f"{pair}=stos")
        elif i == 1:
            _add(t, b + 1, "RET", 10, 1, "jump", "PC=pop")
        elif i == 3:
            _add(t, b + 1, "RET", 10, 1, "jump", "nieudokumentowany (jak RET)")
        elif i == 5:
            _add(t, b + 1, "PCHL", 5, 1, "jump", "PC=HL")
        else:
            _add(t, b + 1, "SPHL", 5, 1, "load", "SP=HL")
        _add(t, b + 2, f"J{CC[i]} a16", 10, 3, "jump", f"skok gdy {CC[i]}")
        col3 = [("JMP a16", "PC=a16", 10, 3, "jump"), ("JMP a16", "nieudokumentowany (jak JMP a16)", 10, 3, "jump"),
                ("OUT d8", "port[d8]=A", 10, 2, "io"), ("IN d8", "A=port[d8]", 10, 2, "io"),
                ("XTHL", "swap HL ze szczytem stosu", 18, 1, "load"), ("XCHG", "swap HL<->DE", 4, 1, "load"),
                ("DI", "INTE=0", 4, 1, "io"), ("EI", "INTE=1 po następnej instrukcji", 4, 1, "io")][i]
        _add(t, b + 3, col3[0], col3[2], col3[3], col3[4], col3[1])
        _add(t, b + 4, f"C{CC[i]} a16", 11, 3, "jump", f"call gdy {CC[i]}; cykle 11/17")
        if i % 2 == 0:
            pair = ["B", "D", "H", "PSW"][i // 2]
            _add(t, b + 5, f"PUSH {pair}", 11, 1, "load", f"stos={pair}")
        else:
            _add(t, b + 5, "CALL a16", 17, 3, "jump",
                 "push PC; PC=a16" if i == 1 else "nieudokumentowany (jak CALL a16)")
        _add(t, b + 6, f"{imm[i][0]} d8", 7, 2, imm[i][2], imm[i][1])
        _add(t, b + 7, f"RST {i}", 11, 1, "jump", f"push PC; PC={i * 8:#04x}")
    assert len(t) == 256, len(t)
    return t


def import_8080() -> None:
    table = build_8080()
    write_json(
        "8080",
        "Intel 8080 — pełna mapa ISA (256 opcodów; nieudokumentowane też zdefiniowane)",
        "Wygenerowane z cpu-vibe-008/docs/8080/opcodes.md (Intel 8080 Complete Opcode Reference) "
        "+ Intel manual cycles. Rejestry: B=0..A=7; M=[HL]. Dla Ccc/Rcc cykle bazowe (11/5), "
        "wzięte w semantics.",
        table,
    )


# --- Motorola MC6800 --------------------------------------------------------
# Grupy MC6800 nie pokrywają się z 6502 — osobna mapa. Cykle CPX/LDX/STX:
# personal-004 ma błąd, wartości wg Motorola (cpu-vibe-012/docs/m6800/03-opcode-map*).

_GROUP_6800 = {
    "flags": "CLV SEV CLC SEC CLI SEI",
    "transfer": "TAP TPA TAB TBA TSX TXS",
    "arithmetic": "SBA ABA DAA INX DEX NEG COM LSR ROR ASR ASL ROL DEC INC TST CLR",
    "compare": "CBA CPX CMPA CMPB",
    "branch": "BRA BRN BHI BLS BCC BCS BNE BEQ BVC BVS BPL BMI BGE BLT BGT BLE",
    "stack": "INS DES PULA PULB PSHA PSHB",
    "jump": "RTS RTI JMP JSR",
    "system": "WAI SWI NOP",
    "load": "LDX STX LDAA LDAB STAA STAB LDS STS",
}
GROUP_6800 = {m: g for g, ms in _GROUP_6800.items() for m in ms.split()}
CYCLES_6800 = {0x8C: 3, 0x9C: 4, 0xBC: 5, 0xDE: 4, 0xEF: 7, 0xFE: 5}


def build_6800() -> list[dict]:
    txt = read("PetEmulator.Cpu6800/M6800Cpu.Mc6800Opcodes.cs")
    table = {}
    for m in re.finditer(
        r'Set\(0x([0-9A-Fa-f]{2}),\s*"([^"]+)",\s*M6800AddressingMode\.(\w+),\s*(\d+),\s*(\d+),', txt
    ):
        op = int(m.group(1), 16)
        name, mode = m.group(2), m.group(3)
        table[op] = entry(op, name, CYCLES_6800.get(op, int(m.group(5))), int(m.group(4)),
                          GROUP_6800.get(name, "undefined"), mode, None, mode)
    table[0x21] = entry(0x21, "BRN", 4, 2, "branch", "Relative", None, "Relative")
    assert len(table) == 56, len(table)
    return [table[k] for k in sorted(table)]


def import_6800() -> None:
    write_json(
        "6800",
        "Motorola MC6800 — podzbiór jawnie zdefiniowany (56 opcodów)",
        "Zaimportowane z personal-004 PetEmulator.Cpu6800; grupy wg Motorola, cykle "
        "CPX/LDX/STX poprawione (0x8C/9C/BC/DE/EF/FE), dodane 21 BRN.",
        build_6800(),
    )


ADAPTERS = {"6502": import_6502, "65c02": import_65c02, "8080": import_8080, "6800": import_6800}


def selftest() -> None:
    core = parse_6502_core()
    assert core[0xA9]["mnemonic"] == "LDA" and core[0xA9]["words"] == 2 and core[0xA9]["cycles"] == 2
    assert core[0xEA]["mnemonic"] == "NOP" and core[0xEA]["cycles"] == 2
    assert core[0x4C]["mnemonic"] == "JMP" and core[0x4C]["words"] == 3
    assert core[0x00]["mnemonic"] == "BRK" and core[0x00]["cycles"] == 7
    assert core[0x03]["mnemonic"] == "SLO" and core[0x03]["cycles"] == 8
    assert core[0x07]["cycles"] == 5 and core[0x0F]["cycles"] == 6 and core[0x13]["cycles"] == 8
    assert core[0x02]["mnemonic"] == "KIL" and core[0x02]["cycles"] == 2
    assert core[0xA3]["cycles"] == 6 and core[0x87]["cycles"] == 3 and core[0xE3]["cycles"] == 8
    assert core[0xA9]["operands"] == [{"name": "d8", "type": "immediate8"}]
    assert core[0xA5]["operands"] == [{"name": "zp", "type": "address8"}]
    assert core[0xAD]["operands"][0]["type"] == "address16"
    assert core[0x03]["semantics"] == "M = ASL(M); A = A | M"
    assert core[0x82]["mnemonic"] == "NOP" and core[0x82]["cycles"] == 2
    intel = {int(i["opcode"], 16): i for i in build_8080()}
    assert len(intel) == 256
    assert intel[0x03]["mnemonic"] == "INX B" and intel[0x03]["cycles"] == 5
    assert intel[0x04]["mnemonic"] == "INR B" and intel[0x06]["mnemonic"] == "MVI B,d8"
    assert intel[0x76]["mnemonic"] == "HLT"
    assert intel[0x80]["mnemonic"] == "ADD B" and intel[0x80]["cycles"] == 4
    assert intel[0x86]["mnemonic"] == "ADD M" and intel[0x86]["cycles"] == 7
    assert intel[0xC3]["mnemonic"] == "JMP a16" and intel[0xE9]["mnemonic"] == "PCHL"
    assert intel[0x08]["mnemonic"] == "NOP" and intel[0x08]["group"] == "basic"
    assert intel[0xCB]["group"] == "jump"
    assert "8080" not in {i["mnemonic"] for i in intel.values()}
    m68 = {int(i["opcode"], 16): i for i in build_6800()}
    assert len(m68) == 56 and m68[0x21]["mnemonic"] == "BRN"
    assert m68[0x8C]["cycles"] == 3 and m68[0xEF]["cycles"] == 7 and m68[0xFE]["cycles"] == 5
    assert m68[0x0B]["group"] == "flags" and m68[0x10]["group"] == "arithmetic"
    assert m68[0x16]["group"] == "transfer" and m68[0x31]["group"] == "stack"
    c02 = {int(i["opcode"], 16): i for i in build_65c02()}
    assert c02[0x07]["mnemonic"] == "RMB0" and c02[0x87]["mnemonic"] == "SMB0"
    assert c02[0x0F]["mnemonic"] == "BBR0" and c02[0xFF]["mnemonic"] == "BBS7"
    assert c02[0xCB]["mnemonic"] == "WAI" and c02[0xDB]["mnemonic"] == "STP"
    assert c02[0x1E]["cycles"] == 6 and c02[0x00]["words"] == 2
    assert c02[0x89]["semantics"].startswith("Z = (A & M) == 0")
    assert c02[0x04]["semantics"].startswith("Z = ((A & M) == 0)")
    print("selftest OK")


def main(argv: list[str]) -> int:
    if argv and argv[0] == "selftest":
        selftest()
        return 0
    for name in argv or list(ADAPTERS):
        if name not in ADAPTERS:
            print(f"nieznany CPU: {name} (dostępne: {', '.join(ADAPTERS)})", file=sys.stderr)
            return 2
        ADAPTERS[name]()
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
