#!/usr/bin/env python3
"""Generate the documented Motorola 6809 instruction-family table."""

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "data" / "instructions" / "mcp_6809_instructions.json"

# Opcode families from LWTools 4.25 instab.c; cycles follow Motorola M6809PM Appendix F
# (indexed postbyte and taken-branch penalties are variable; see the generated notes).
# opcode|mnemonic|mode|cycles|bytes with a one-byte opcode|group.
ROWS = """
00|NEG|direct|6|2|arithmetic
03|COM|direct|6|2|arithmetic
04|LSR|direct|6|2|arithmetic
06|ROR|direct|6|2|arithmetic
07|ASR|direct|6|2|arithmetic
09|ROL|direct|6|2|arithmetic
0A|DEC|direct|6|2|arithmetic
0C|INC|direct|6|2|arithmetic
0D|TST|direct|6|2|arithmetic
0E|JMP|direct|3|2|jump
0F|CLR|direct|6|2|arithmetic
12|NOP|inherent|2|1|system
13|SYNC|inherent|2|1|system
16|LBRA|relative16|5|4|branch
17|LBSR|relative16|9|4|jump
19|DAA|inherent|2|1|system
1A|ORCC|immediate8|3|2|system
1C|ANDCC|immediate8|3|2|system
1D|SEX|inherent|2|1|system
1E|EXG|registerpair|8|2|system
1F|TFR|registerpair|7|2|system
20|BRA|relative8|3|2|branch
21|BRN|relative8|3|2|branch
22|BHI|relative8|3|2|branch
23|BLS|relative8|3|2|branch
24|BCC|relative8|3|2|branch
25|BCS|relative8|3|2|branch
26|BNE|relative8|3|2|branch
27|BEQ|relative8|3|2|branch
28|BVC|relative8|3|2|branch
29|BVS|relative8|3|2|branch
2A|BPL|relative8|3|2|branch
2B|BMI|relative8|3|2|branch
2C|BGE|relative8|3|2|branch
2D|BLT|relative8|3|2|branch
2E|BGT|relative8|3|2|branch
2F|BLE|relative8|3|2|branch
30|LEAX|indexed|4|2|load
31|LEAY|indexed|4|2|load
32|LEAS|indexed|4|2|load
33|LEAU|indexed|4|2|load
34|PSHS|registermask|5|2|stack
35|PULS|registermask|5|2|stack
36|PSHU|registermask|5|2|stack
37|PULU|registermask|5|2|stack
39|RTS|inherent|5|1|jump
3A|ABX|inherent|3|1|arithmetic
3B|RTI|inherent|6|1|system
3C|CWAI|immediate8|20|2|system
3D|MUL|inherent|11|1|arithmetic
3F|SWI|inherent|19|1|system
40|NEGA|inherent|2|1|arithmetic
43|COMA|inherent|2|1|arithmetic
44|LSRA|inherent|2|1|arithmetic
46|RORA|inherent|2|1|arithmetic
47|ASRA|inherent|2|1|arithmetic
48|ASLA|inherent|2|1|arithmetic
49|ROLA|inherent|2|1|arithmetic
4A|DECA|inherent|2|1|arithmetic
4C|INCA|inherent|2|1|arithmetic
4D|TSTA|inherent|2|1|arithmetic
4F|CLRA|inherent|2|1|arithmetic
50|NEGB|inherent|2|1|arithmetic
53|COMB|inherent|2|1|arithmetic
54|LSRB|inherent|2|1|arithmetic
56|RORB|inherent|2|1|arithmetic
57|ASRB|inherent|2|1|arithmetic
58|ASLB|inherent|2|1|arithmetic
59|ROLB|inherent|2|1|arithmetic
5A|DECB|inherent|2|1|arithmetic
5C|INCB|inherent|2|1|arithmetic
5D|TSTB|inherent|2|1|arithmetic
5F|CLRB|inherent|2|1|arithmetic
60|NEG|indexed|6|2|arithmetic
63|COM|indexed|6|2|arithmetic
64|LSR|indexed|6|2|arithmetic
66|ROR|indexed|6|2|arithmetic
67|ASR|indexed|6|2|arithmetic
68|ASL|indexed|6|2|arithmetic
69|ROL|indexed|6|2|arithmetic
6A|DEC|indexed|6|2|arithmetic
6C|INC|indexed|6|2|arithmetic
6D|TST|indexed|6|2|arithmetic
6E|JMP|indexed|3|2|jump
6F|CLR|indexed|6|2|arithmetic
70|NEG|extended|7|3|arithmetic
73|COM|extended|7|3|arithmetic
74|LSR|extended|7|3|arithmetic
76|ROR|extended|7|3|arithmetic
77|ASR|extended|7|3|arithmetic
78|ASL|extended|7|3|arithmetic
79|ROL|extended|7|3|arithmetic
7A|DEC|extended|7|3|arithmetic
7C|INC|extended|7|3|arithmetic
7D|TST|extended|7|3|arithmetic
7E|JMP|extended|3|3|jump
7F|CLR|extended|7|3|arithmetic
80|SUBA|immediate8|2|2|arithmetic
81|CMPA|immediate8|2|2|compare
82|SBCA|immediate8|2|2|arithmetic
83|SUBD|immediate16|4|3|arithmetic
84|ANDA|immediate8|2|2|logic
85|BITA|immediate8|2|2|compare
86|LDA|immediate8|2|2|load
88|EORA|immediate8|2|2|logic
89|ADCA|immediate8|2|2|arithmetic
8A|ORA|immediate8|2|2|logic
8B|ADDA|immediate8|2|2|arithmetic
8C|CMPX|immediate16|4|3|compare
8D|BSR|relative8|7|2|jump
8E|LDX|immediate16|3|3|load
90|SUBA|direct|4|2|arithmetic
91|CMPA|direct|4|2|compare
92|SBCA|direct|4|2|arithmetic
93|SUBD|direct|6|2|arithmetic
94|ANDA|direct|4|2|logic
95|BITA|direct|4|2|compare
96|LDA|direct|4|2|load
97|STA|direct|4|2|load
98|EORA|direct|4|2|logic
99|ADCA|direct|4|2|arithmetic
9A|ORA|direct|4|2|logic
9B|ADDA|direct|4|2|arithmetic
9C|CMPX|direct|6|2|compare
9D|JSR|direct|7|2|jump
9E|LDX|direct|5|2|load
9F|STX|direct|5|2|load
A0|SUBA|indexed|4|2|arithmetic
A1|CMPA|indexed|4|2|compare
A2|SBCA|indexed|4|2|arithmetic
A3|SUBD|indexed|6|2|arithmetic
A4|ANDA|indexed|4|2|logic
A5|BITA|indexed|4|2|compare
A6|LDA|indexed|4|2|load
A7|STA|indexed|4|2|load
A8|EORA|indexed|4|2|logic
A9|ADCA|indexed|4|2|arithmetic
AA|ORA|indexed|4|2|logic
AB|ADDA|indexed|4|2|arithmetic
AC|CMPX|indexed|6|2|compare
AD|JSR|indexed|7|2|jump
AE|LDX|indexed|5|2|load
AF|STX|indexed|5|2|load
B0|SUBA|extended|5|3|arithmetic
B1|CMPA|extended|5|3|compare
B2|SBCA|extended|5|3|arithmetic
B3|SUBD|extended|7|3|arithmetic
B4|ANDA|extended|5|3|logic
B5|BITA|extended|5|3|compare
B6|LDA|extended|5|3|load
B7|STA|extended|5|3|load
B8|EORA|extended|5|3|logic
B9|ADCA|extended|5|3|arithmetic
BA|ORA|extended|5|3|logic
BB|ADDA|extended|5|3|arithmetic
BC|CMPX|extended|7|3|compare
BD|JSR|extended|8|3|jump
BE|LDX|extended|6|3|load
BF|STX|extended|6|3|load
C0|SUBB|immediate8|2|2|arithmetic
C1|CMPB|immediate8|2|2|compare
C2|SBCB|immediate8|2|2|arithmetic
C3|ADDD|immediate16|4|3|arithmetic
C4|ANDB|immediate8|2|2|logic
C5|BITB|immediate8|2|2|compare
C6|LDB|immediate8|2|2|load
C8|EORB|immediate8|2|2|logic
C9|ADCB|immediate8|2|2|arithmetic
CA|ORB|immediate8|2|2|logic
CB|ADDB|immediate8|2|2|arithmetic
CC|LDD|immediate16|3|3|load
CE|LDU|immediate16|3|3|load
D0|SUBB|direct|4|2|arithmetic
D1|CMPB|direct|4|2|compare
D2|SBCB|direct|4|2|arithmetic
D3|ADDD|direct|6|2|arithmetic
D4|ANDB|direct|4|2|logic
D5|BITB|direct|4|2|compare
D6|LDB|direct|4|2|load
D7|STB|direct|4|2|load
D8|EORB|direct|4|2|logic
D9|ADCB|direct|4|2|arithmetic
DA|ORB|direct|4|2|logic
DB|ADDB|direct|4|2|arithmetic
DC|LDD|direct|5|2|load
DD|STD|direct|5|2|load
DE|LDU|direct|5|2|load
DF|STU|direct|5|2|load
E0|SUBB|indexed|4|2|arithmetic
E1|CMPB|indexed|4|2|compare
E2|SBCB|indexed|4|2|arithmetic
E3|ADDD|indexed|6|2|arithmetic
E4|ANDB|indexed|4|2|logic
E5|BITB|indexed|4|2|compare
E6|LDB|indexed|4|2|load
E7|STB|indexed|4|2|load
E8|EORB|indexed|4|2|logic
E9|ADCB|indexed|4|2|arithmetic
EA|ORB|indexed|4|2|logic
EB|ADDB|indexed|4|2|arithmetic
EC|LDD|indexed|5|2|load
ED|STD|indexed|5|2|load
EE|LDU|indexed|5|2|load
EF|STU|indexed|5|2|load
F0|SUBB|extended|5|3|arithmetic
F1|CMPB|extended|5|3|compare
F2|SBCB|extended|5|3|arithmetic
F3|ADDD|extended|7|3|arithmetic
F4|ANDB|extended|5|3|logic
F5|BITB|extended|5|3|compare
F6|LDB|extended|5|3|load
F7|STB|extended|5|3|load
F8|EORB|extended|5|3|logic
F9|ADCB|extended|5|3|arithmetic
FA|ORB|extended|5|3|logic
FB|ADDB|extended|5|3|arithmetic
FC|LDD|extended|6|3|load
FD|STD|extended|6|3|load
FE|LDU|extended|6|3|load
FF|STU|extended|6|3|load
1021|LBRN|relative16|5|4|branch
1022|LBHI|relative16|5|4|branch
1023|LBLS|relative16|5|4|branch
1024|LBCC|relative16|5|4|branch
1025|LBCS|relative16|5|4|branch
1026|LBNE|relative16|5|4|branch
1027|LBEQ|relative16|5|4|branch
1028|LBVC|relative16|5|4|branch
1029|LBVS|relative16|5|4|branch
102A|LBPL|relative16|5|4|branch
102B|LBMI|relative16|5|4|branch
102C|LBGE|relative16|5|4|branch
102D|LBLT|relative16|5|4|branch
102E|LBGT|relative16|5|4|branch
102F|LBLE|relative16|5|4|branch
103F|SWI2|inherent|20|1|system
1083|CMPD|immediate16|5|3|compare
108C|CMPY|immediate16|5|3|compare
108E|LDY|immediate16|4|3|load
1093|CMPD|direct|7|2|compare
109C|CMPY|direct|7|2|compare
109E|LDY|direct|6|2|load
109F|STY|direct|6|2|load
10A3|CMPD|indexed|7|2|compare
10AC|CMPY|indexed|7|2|compare
10AE|LDY|indexed|6|2|load
10AF|STY|indexed|6|2|load
10B3|CMPD|extended|8|3|compare
10BC|CMPY|extended|8|3|compare
10BE|LDY|extended|7|3|load
10BF|STY|extended|7|3|load
10CE|LDS|immediate16|4|3|load
10DE|LDS|direct|6|2|load
10DF|STS|direct|6|2|load
10EE|LDS|indexed|6|2|load
10EF|STS|indexed|6|2|load
10FE|LDS|extended|7|3|load
10FF|STS|extended|7|3|load
113F|SWI3|inherent|20|1|system
1183|CMPU|immediate16|5|3|compare
118C|CMPS|immediate16|5|3|compare
1193|CMPU|direct|7|2|compare
119C|CMPS|direct|7|2|compare
11A3|CMPU|indexed|7|2|compare
11AC|CMPS|indexed|7|2|compare
11B3|CMPU|extended|8|3|compare
11BC|CMPS|extended|8|3|compare
""".strip()

OPERANDS = {
    "direct": ("d8", [{"name": "d8", "type": "address8"}]),
    "extended": ("a16", [{"name": "a16", "type": "address16"}]),
    "indexed": ("indexed", [{"name": "indexed", "type": "indexed"}]),
    "immediate8": ("#d8", [{"name": "d8", "type": "immediate8"}]),
    "immediate16": ("#d16", [{"name": "d16", "type": "immediate16"}]),
    "relative8": ("rel", [{"name": "rel", "type": "relative8"}]),
    "relative16": ("rel16", [{"name": "rel16", "type": "relative16"}]),
    "registerpair": (
        "src,dst",
        [{"name": "src", "type": "register"}, {"name": "dst", "type": "register"}],
    ),
    "registermask": ("registers", [{"name": "registers", "type": "register-list"}]),
}


def generate() -> list[dict]:
    instructions = []
    seen: set[str] = set()
    for row in ROWS.splitlines():
        opcode, mnemonic, mode, cycles, words, group = row.split("|")
        if opcode in seen:
            raise ValueError(f"duplicate opcode {opcode}")
        seen.add(opcode)
        operand_template, operands = OPERANDS.get(mode, ("", None))
        instructions.append(
            {
                "opcode": opcode,
                "mnemonic": mnemonic
                if not operand_template
                else f"{mnemonic} {operand_template}",
                "cycles": int(cycles),
                "words": int(words)
                - (mode == "relative16")
                + len(bytes.fromhex(opcode))
                - 1,
                "semantics": None,
                "group": group,
                "encoding": mode,
                "operands": operands,
                "variants": None,
            }
        )
    return instructions


def main() -> None:
    data = {
        "$schema": "isa.schema.json",
        "processor": "M6809",
        "format_version": "1.0",
        "description": "Motorola 6809 (udokumentowane instrukcje; bez 6309).",
        "notes": "Opcode zawiera prefiksy 10/11. Cykle stałe wg Motorola M6809PM Appendix F; TFR=7, JMP extended=3, CWAI=20. Indexed: words/cycles podają bazę; postbyte może dodać bajty/cykle (Appendix F-2). Warunkowy long branch ma +1 cykl, gdy jest wzięty. LBxx to long-relative. Direct page=$00 ustawiana przez crt0.",
        "instructions": generate(),
    }
    OUTPUT.write_text(
        json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(
        f"6809: {len(data['instructions'])} opcode families -> {OUTPUT.relative_to(ROOT)}"
    )


if __name__ == "__main__":
    main()
