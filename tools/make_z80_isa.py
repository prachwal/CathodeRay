#!/usr/bin/env python3
"""Generuje data/instructions/mcp_z80_instructions.json z reguł dekodowania Z80 i weryfikuje bajty z z80asm.

Struktura opcode'u Z80: x = op>>6, y = (op>>3)&7, z = op&7, p = y>>1, q = y&1 (tabele jak w "Decoding Z80 opcodes",
z80.info). Prefiksy: CB (bity i rotacje), ED (rozszerzone), DD/FD (HL -> IX/IY, (HL) -> (IX+d)), DDCB/FDCB.
Cykle (T-states) z Zilog UM0080; dla instrukcji warunkowych wartość bez skoku, wariant ze skokiem w semantics.

Szablon w polu mnemonic w składni Zilog z placeholderami: n (bajt), nn (słowo), e (skok względny), d (przesunięcie IX/IY).
Wpisy nieudokumentowane mają variants = "undocumented".

Weryfikacja: każdy wpis asemblowany osobno przez z80asm (z88dk, -mz80) z przykładowymi wartościami; bajty muszą się zgadzać.
Wpis udokumentowany odrzucony przez z80asm albo o innych bajtach przerywa generowanie. Nieudokumentowany duplikat,
dla którego z80asm daje bajty udokumentowanego wpisu o tym samym szablonie (NEG ED4C -> ED44), to alias i przechodzi;
nieudokumentowane, których z80asm nie zna, trafiają na listę w podsumowaniu.

Uruchamiać z katalogu repo: python3 tools/make_z80_isa.py [--no-verify]
"""
import json
import os
import pathlib
import re
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = ROOT / "data" / "instructions" / "mcp_z80_instructions.json"

R = ["B", "C", "D", "E", "H", "L", "(HL)", "A"]
RP = ["BC", "DE", "HL", "SP"]
RP2 = ["BC", "DE", "HL", "AF"]
CC = ["NZ", "Z", "NC", "C", "PO", "PE", "P", "M"]
ALU = ["ADD A,", "ADC A,", "SUB ", "SBC A,", "AND ", "XOR ", "OR ", "CP "]
ROT = ["RLC", "RRC", "RL", "RR", "SLA", "SRA", "SLL", "SRL"]
BLOCK = {
    (4, 0): "LDI", (4, 1): "CPI", (4, 2): "INI", (4, 3): "OUTI",
    (5, 0): "LDD", (5, 1): "CPD", (5, 2): "IND", (5, 3): "OUTD",
    (6, 0): "LDIR", (6, 1): "CPIR", (6, 2): "INIR", (6, 3): "OTIR",
    (7, 0): "LDDR", (7, 1): "CPDR", (7, 2): "INDR", (7, 3): "OTDR",
}


def entry(opcode, template, group, cycles, documented=True, note=None):
    return {"opcode": opcode, "template": template, "group": group, "cycles": cycles, "documented": documented, "note": note}


def unprefixed(index=None):
    """Tabela bez prefiksu; index = 'IX'/'IY' generuje warianty DD/FD (tylko instrukcje z HL, H, L lub (HL))."""
    prefix = [] if index is None else [0xDD if index == "IX" else 0xFD]
    out = []

    def reg(i, memory_partner=False):
        if index is None:
            return R[i]
        if i == 6:
            return f"({index}+d)"
        if i in (4, 5) and not memory_partner:
            return index + ("H" if i == 4 else "L")
        return R[i]

    def rp(i, table=RP):
        return index if (index and table[i] == "HL") else table[i]

    def add(op, template, group, cycles, documented=True, note=None, touches_index=False):
        if index is None:
            out.append(entry(bytes([op]), template, group, cycles, documented, note))
        elif touches_index:
            out.append(entry(bytes(prefix + [op]), template, group, cycles, documented, note))

    for op in range(256):
        x, y, z, p, q = op >> 6, (op >> 3) & 7, op & 7, (op >> 3) >> 1 & 3, (op >> 3) & 1
        if op in (0xCB, 0xDD, 0xED, 0xFD):
            continue
        if x == 0:
            if z == 0:
                if y == 0:
                    add(op, "NOP", "system", 4)
                elif y == 1:
                    add(op, "EX AF,AF'", "load", 4)
                elif y == 2:
                    add(op, "DJNZ e", "jump", 8, note="13 T gdy skok")
                elif y == 3:
                    add(op, "JR e", "jump", 12)
                else:
                    add(op, f"JR {CC[y - 4]},e", "jump", 7, note="12 T gdy skok")
            elif z == 1:
                if q == 0:
                    add(op, f"LD {rp(p)},nn", "load", 14 if rp(p) != RP[p] else 10, touches_index=rp(p) != RP[p])
                else:
                    add(op, f"ADD {rp(2)},{rp(p)}", "arithmetic", 15 if index else 11, touches_index=True)
            elif z == 2:
                table = {
                    (0, 0): ("LD (BC),A", 7, False), (0, 1): ("LD (DE),A", 7, False),
                    (0, 2): (f"LD (nn),{rp(2)}", 20 if index else 16, True), (0, 3): ("LD (nn),A", 13, False),
                    (1, 0): ("LD A,(BC)", 7, False), (1, 1): ("LD A,(DE)", 7, False),
                    (1, 2): (f"LD {rp(2)},(nn)", 20 if index else 16, True), (1, 3): ("LD A,(nn)", 13, False),
                }
                template, cycles, touches = table[(q, p)]
                add(op, template, "load", cycles, touches_index=touches)
            elif z == 3:
                touches = rp(p) != RP[p]
                add(op, f"{'INC' if q == 0 else 'DEC'} {rp(p)}", "arithmetic", 10 if touches else 6, touches_index=touches)
            elif z in (4, 5):
                name = "INC" if z == 4 else "DEC"
                cycles = (23 if index else 11) if y == 6 else (8 if index else 4)
                add(op, f"{name} {reg(y)}", "arithmetic", cycles, documented=not (index and y in (4, 5)), touches_index=y in (4, 5, 6))
            elif z == 6:
                cycles = (19 if index else 10) if y == 6 else (11 if index else 7)
                add(op, f"LD {reg(y)},n", "load", cycles, documented=not (index and y in (4, 5)), touches_index=y in (4, 5, 6))
            else:
                add(op, ["RLCA", "RRCA", "RLA", "RRA", "DAA", "CPL", "SCF", "CCF"][y], "arithmetic" if y in (4, 5) else "logic", 4)
        elif x == 1:
            if y == 6 and z == 6:
                add(op, "HALT", "system", 4)
            else:
                memory = 6 in (y, z)
                touches = bool({y, z} & {4, 5, 6})
                cycles = (19 if index else 7) if memory else (8 if index else 4)
                add(op, f"LD {reg(y, memory)},{reg(z, memory)}", "load", cycles,
                    documented=not (index and touches and not memory), touches_index=touches)
        elif x == 2:
            cycles = (19 if index else 7) if z == 6 else (8 if index else 4)
            add(op, f"{ALU[y]}{reg(z)}", "arithmetic" if y < 4 or y == 7 else "logic", cycles,
                documented=not (index and z in (4, 5)), touches_index=z in (4, 5, 6))
        else:
            if z == 0:
                add(op, f"RET {CC[y]}", "jump", 5, note="11 T gdy skok")
            elif z == 1:
                if q == 0:
                    touches = rp(p, RP2) != RP2[p]
                    add(op, f"POP {rp(p, RP2)}", "stack", 14 if touches else 10, touches_index=touches)
                else:
                    table = {0: ("RET", 10, False), 1: ("EXX", 4, False), 2: (f"JP ({rp(2)})", 8 if index else 4, True),
                             3: (f"LD SP,{rp(2)}", 10 if index else 6, True)}
                    template, cycles, touches = table[p]
                    add(op, template, "jump" if p in (0, 2) else "load", cycles, touches_index=touches)
            elif z == 2:
                add(op, f"JP {CC[y]},nn", "jump", 10)
            elif z == 3:
                table = {0: ("JP nn", 10, "jump", False), 2: ("OUT (n),A", 11, "io", False), 3: ("IN A,(n)", 11, "io", False),
                         4: (f"EX (SP),{rp(2)}", 23 if index else 19, "stack", True), 5: ("EX DE,HL", 4, "load", False),
                         6: ("DI", 4, "system", False), 7: ("EI", 4, "system", False)}
                template, cycles, group, touches = table[y]
                add(op, template, group, cycles, touches_index=touches)
            elif z == 4:
                add(op, f"CALL {CC[y]},nn", "jump", 10, note="17 T gdy skok")
            elif z == 5:
                if q == 0:
                    touches = rp(p, RP2) != RP2[p]
                    add(op, f"PUSH {rp(p, RP2)}", "stack", 15 if touches else 11, touches_index=touches)
                elif p == 0:
                    add(op, "CALL nn", "jump", 17)
            elif z == 6:
                add(op, f"{ALU[y]}n", "arithmetic" if y < 4 or y == 7 else "logic", 7)
            else:
                add(op, f"RST {y * 8:02X}H", "jump", 11)
    return out


def cb(index=None):
    """CB (index=None) albo DDCB/FDCB: bity i rotacje. Na (IX+d) z != 6 to nieudokumentowane warianty
    z kopią wyniku do rejestru (ROT/RES/SET ... ,r) albo duplikaty BIT (IX+d)."""
    out = []
    for op in range(256):
        x, y, z = op >> 6, (op >> 3) & 7, op & 7
        if index is None:
            code, target, suffix = bytes([0xCB, op]), R[z], ""
            memory = z == 6
            cycles = {0: 15, 1: 12}.get(x, 15) if memory else 8
        else:
            code = bytes([0xDD if index == "IX" else 0xFD, 0xCB, op])
            target, suffix = f"({index}+d)", ("" if z == 6 or x == 1 else f",{R[z]}")
            cycles = 20 if x == 1 else 23
        name = {0: ROT[y], 1: f"BIT {y},", 2: f"RES {y},", 3: f"SET {y},"}[x]
        template = f"{name} {target}{suffix}" if x == 0 else f"{name}{target}{suffix}"
        documented = not (x == 0 and y == 6) and (index is None or z == 6)
        note = "duplikat BIT (IX+d)" if index is not None and x == 1 and z != 6 else None
        out.append(entry(code, template, "logic" if x == 0 else "bit", cycles, documented, note))
    return out


def ed():
    out = []
    im = {0: "0", 1: "0", 2: "1", 3: "2", 4: "0", 5: "0", 6: "1", 7: "2"}
    for op in range(256):
        x, y, z, p, q = op >> 6, (op >> 3) & 7, op & 7, (op >> 3) >> 1 & 3, (op >> 3) & 1
        code = bytes([0xED, op])
        if x == 1:
            if z == 0:
                out.append(entry(code, "IN F,(C)" if y == 6 else f"IN {R[y]},(C)", "io", 12, y != 6))
            elif z == 1:
                out.append(entry(code, "OUT (C),0" if y == 6 else f"OUT (C),{R[y]}", "io", 12, y != 6))
            elif z == 2:
                out.append(entry(code, f"{'SBC' if q == 0 else 'ADC'} HL,{RP[p]}", "arithmetic", 15))
            elif z == 3:
                out.append(entry(code, f"LD (nn),{RP[p]}" if q == 0 else f"LD {RP[p]},(nn)", "load", 20, p != 2))
            elif z == 4:
                out.append(entry(code, "NEG", "arithmetic", 8, y == 0))
            elif z == 5:
                out.append(entry(code, "RETI" if y == 1 else "RETN", "jump", 14, y in (0, 1)))
            elif z == 6:
                out.append(entry(code, f"IM {im[y]}", "system", 8, y in (0, 2, 3)))
            else:
                table = ["LD I,A", "LD R,A", "LD A,I", "LD A,R", "RRD", "RLD", "NOP", "NOP"]
                cycles = [9, 9, 9, 9, 18, 18, 8, 8][y]
                out.append(entry(code, table[y], "load" if y < 4 else ("logic" if y < 6 else "system"), cycles, y < 6))
        elif x == 2 and (y, z) in BLOCK:
            repeat = y >= 6
            out.append(entry(code, BLOCK[(y, z)], "block", 16, note="21 T gdy powtarza (BC<>0 / B<>0)" if repeat else None))
    return out


def all_entries():
    return unprefixed() + unprefixed("IX") + unprefixed("IY") + cb() + cb("IX") + cb("IY") + ed()


PLACEHOLDER = re.compile(r"\b(nn|n|e)\b|\+d\)")
SAMPLE = {"nn": "1234h", "n": "12h", "e": "$+5", "+d)": "+5)"}


def encoded(e):
    """Oczekiwane bajty dla przykładowych wartości (n=12h, nn=1234h, e=$+5, d=+5)."""
    fields = []
    for m in PLACEHOLDER.finditer(e["template"]):
        token = m.group(0)
        fields += {"nn": [0x34, 0x12], "n": [0x12], "e": [0x05 - len_of(e)], "+d)": [0x05]}[token]
    op = list(e["opcode"])
    if len(op) == 3 and op[1] == 0xCB:
        return bytes(op[:2] + fields + op[2:])
    return bytes(op + fields)


def len_of(e):
    size = len(e["opcode"])
    for m in PLACEHOLDER.finditer(e["template"]):
        size += 2 if m.group(0) == "nn" else 1
    return size


def sample_line(e):
    return PLACEHOLDER.sub(lambda m: SAMPLE[m.group(0)], e["template"])


def z80asm(lines):
    env = dict(os.environ, PATH="/opt/z88dk-2.4/bin:" + os.environ["PATH"], ZCCCFG="/opt/z88dk-2.4/lib/config")
    with tempfile.TemporaryDirectory() as tmp:
        t = pathlib.Path(tmp)
        (t / "p.asm").write_text("".join(f"\t{line}\n" for line in lines))
        run = subprocess.run(["z80asm", "-mz80", "-b", "p.asm"], cwd=t, env=env, capture_output=True, text=True)
        return (t / "p.bin").read_bytes() if run.returncode == 0 and (t / "p.bin").exists() else None


def verify(entries):
    canonical = {e["template"]: encoded(e) for e in entries if e["documented"]}
    unknown = []
    for e in entries:
        got = z80asm([sample_line(e)])
        want = encoded(e)
        if got == want:
            continue
        if not e["documented"] and got is None:
            unknown.append(sample_line(e))
            continue
        if not e["documented"] and got == canonical.get(e["template"]):
            continue
        raise SystemExit(f"{e['opcode'].hex().upper()} {e['template']}: z80asm {got.hex() if got else 'rejects'}, expected {want.hex()}")
    return unknown


def operands(template):
    types = {"nn": "immediate16", "n": "immediate8", "e": "relative8", "+d)": "displacement8"}
    found = [types[m.group(0)] for m in PLACEHOLDER.finditer(template)]
    names = {"immediate16": "nn", "immediate8": "n", "relative8": "e", "displacement8": "d"}
    return [{"name": names[t], "type": t} for t in found] or None


def to_json(entries):
    instructions = []
    for e in entries:
        op = e["opcode"]
        encoding = " ".join(f"{b:02X}" for b in op)
        if len(op) == 3 and op[1] == 0xCB:
            encoding = f"{op[0]:02X} CB d {op[2]:02X}"
        else:
            encoding += "".join(" " + {"nn": "nn", "n": "n", "e": "e", "+d)": "d"}[m.group(0)] for m in PLACEHOLDER.finditer(e["template"]))
        instructions.append({
            "opcode": op.hex().upper(),
            "mnemonic": e["template"],
            "cycles": e["cycles"],
            "words": len_of(e),
            "semantics": e["note"],
            "group": e["group"],
            "encoding": encoding,
            "operands": operands(e["template"]),
            "variants": None if e["documented"] else "undocumented",
        })
    return {
        "$schema": "isa.schema.json",
        "processor": "Z80",
        "format_version": "1.0",
        "description": "Zilog Z80: pełna tabela opcode'ów (bez prefiksu, CB, ED, DD/FD, DDCB/FDCB) wygenerowana z reguł dekodowania.",
        "notes": "Wygenerowane przez tools/make_z80_isa.py; bajty zweryfikowane z z80asm (z88dk). mnemonic = szablon Zilog: n bajt, nn słowo, "
                 "e skok względny, d przesunięcie (IX+d)/(IY+d). DDCB/FDCB: przesunięcie przed ostatnim bajtem (DD CB d op). "
                 "variants = undocumented dla instrukcji nieudokumentowanych. Cykle: Zilog UM0080, warunkowe bez skoku (skok w semantics).",
        "instructions": instructions,
    }


def main():
    entries = all_entries()
    if "--no-verify" not in sys.argv:
        unknown = verify(entries)
        print(f"z80asm: {len(entries) - len(unknown)} zweryfikowanych, {len(unknown)} nieudokumentowanych nieznanych z80asm")
        for line in unknown:
            print("   ", line)
    documented = [e for e in entries if e["documented"]]
    print(f"wpisy: {len(entries)} (udokumentowane {len(documented)})")
    OUT.write_text(json.dumps(to_json(entries), indent=2, ensure_ascii=False) + "\n")


if __name__ == "__main__":
    main()
