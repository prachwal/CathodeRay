#!/usr/bin/env python3
"""Generuje data/instructions/mcp_6800_instructions.json (Motorola 6800).

Format jak mcp_8080_instructions.json; pole `mnemonic` to szablon składni
Motoroli: `#d8` = natychmiastowy 8-bit, `#d16` = natychmiastowy 16-bit
(LDX/LDS/CPX), `d8` = strona zerowa (direct), `a16` = rozszerzony,
`d8,X` = indeksowany, `rel` = skok względny.

Każdy wpis weryfikowany bajt w bajt przeciw as6800 przez golden Asm/6800
(tools/make_asm_golden.py tego nie robi — tabela poniżej jest źródłem).
"""

import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = ROOT / "data" / "instructions" / "mcp_6800_instructions.json"

# (opcode, szablon, cykle, grupa)
ROWS = """
01 NOP 2 system
06 TAP 2 system
07 TPA 2 system
08 INX 4 system
09 DEX 4 system
0A CLV 2 system
0B SEV 2 system
0C CLC 2 system
0D SEC 2 system
0E CLI 2 system
0F SEI 2 system
10 SBA 2 arithmetic
11 CBA 2 compare
16 TAB 2 load
17 TBA 2 load
19 DAA 2 arithmetic
1B ABA 2 arithmetic
30 TSX 4 system
31 INS 4 system
34 DES 4 system
35 TXS 4 system
39 RTS 5 jump
3B RTI 10 system
3E WAI 9 system
3F SWI 12 system
40 NEGA 2 arithmetic
43 COMA 2 arithmetic
44 LSRA 2 arithmetic
46 RORA 2 arithmetic
47 ASRA 2 arithmetic
48 ASLA 2 arithmetic
49 ROLA 2 arithmetic
4A DECA 2 arithmetic
4C INCA 2 arithmetic
4D TSTA 2 arithmetic
4F CLRA 2 arithmetic
50 NEGB 2 arithmetic
53 COMB 2 arithmetic
54 LSRB 2 arithmetic
56 RORB 2 arithmetic
57 ASRB 2 arithmetic
58 ASLB 2 arithmetic
59 ROLB 2 arithmetic
5A DECB 2 arithmetic
5C INCB 2 arithmetic
5D TSTB 2 arithmetic
5F CLRB 2 arithmetic
70 NEG a16 6 arithmetic
73 COM a16 6 arithmetic
74 LSR a16 6 arithmetic
76 ROR a16 6 arithmetic
77 ASR a16 6 arithmetic
78 ASL a16 6 arithmetic
79 ROL a16 6 arithmetic
7A DEC a16 6 arithmetic
7C INC a16 6 arithmetic
7D TST a16 6 arithmetic
7E JMP a16 3 jump
7F CLR a16 6 arithmetic
60 NEG d8,X 7 arithmetic
63 COM d8,X 7 arithmetic
64 LSR d8,X 7 arithmetic
66 ROR d8,X 7 arithmetic
67 ASR d8,X 7 arithmetic
68 ASL d8,X 7 arithmetic
69 ROL d8,X 7 arithmetic
6A DEC d8,X 7 arithmetic
6C INC d8,X 7 arithmetic
6D TST d8,X 7 arithmetic
6E JMP d8,X 3 jump
6F CLR d8,X 7 arithmetic
80 SUBA #d8 2 arithmetic
81 CMPA #d8 2 compare
82 SBCA #d8 2 arithmetic
84 ANDA #d8 2 logic
85 BITA #d8 2 logic
86 LDAA #d8 2 load
88 EORA #d8 2 logic
8A ORAA #d8 2 logic
8B ADDA #d8 2 arithmetic
8C CPX #d16 3 compare
8D BSR rel 8 jump
8E LDS #d16 3 load
90 SUBA d8 3 arithmetic
91 CMPA d8 3 compare
92 SBCA d8 3 arithmetic
94 ANDA d8 3 logic
95 BITA d8 3 logic
96 LDAA d8 3 load
97 STAA d8 4 load
98 EORA d8 3 logic
9A ORAA d8 3 logic
9B ADDA d8 3 arithmetic
9C CPX d8 4 compare
9E LDS d8 4 load
9F STS d8 5 load
A0 SUBA d8,X 5 arithmetic
A1 CMPA d8,X 5 compare
A2 SBCA d8,X 5 arithmetic
A4 ANDA d8,X 5 logic
A5 BITA d8,X 5 logic
A6 LDAA d8,X 5 load
A7 STAA d8,X 6 load
A8 EORA d8,X 5 logic
AA ORAA d8,X 5 logic
AB ADDA d8,X 5 arithmetic
AC CPX d8,X 6 compare
AD JSR d8,X 8 jump
AE LDS d8,X 6 load
AF STS d8,X 7 load
B0 SUBA a16 4 arithmetic
B1 CMPA a16 4 compare
B2 SBCA a16 4 arithmetic
B4 ANDA a16 4 logic
B5 BITA a16 4 logic
B6 LDAA a16 4 load
B7 STAA a16 5 load
B8 EORA a16 4 logic
BA ORAA a16 4 logic
BB ADDA a16 4 arithmetic
BC CPX a16 5 compare
BD JSR a16 9 jump
BE LDS a16 5 load
BF STS a16 6 load
C0 SUBB #d8 2 arithmetic
C1 CMPB #d8 2 compare
C2 SBCB #d8 2 arithmetic
C4 ANDB #d8 2 logic
C5 BITB #d8 2 logic
C6 LDAB #d8 2 load
C8 EORB #d8 2 logic
CA ORAB #d8 2 logic
CB ADDB #d8 2 arithmetic
CE LDX #d16 3 load
D0 SUBB d8 3 arithmetic
D1 CMPB d8 3 compare
D2 SBCB d8 3 arithmetic
D4 ANDB d8 3 logic
D5 BITB d8 3 logic
D6 LDAB d8 3 load
D7 STAB d8 4 load
D8 EORB d8 3 logic
DA ORAB d8 3 logic
DB ADDB d8 3 arithmetic
DE LDX d8 4 load
DF STX d8 5 load
E0 SUBB d8,X 5 arithmetic
E1 CMPB d8,X 5 compare
E2 SBCB d8,X 5 arithmetic
E4 ANDB d8,X 5 logic
E5 BITB d8,X 5 logic
E6 LDAB d8,X 5 load
E7 STAB d8,X 6 load
E8 EORB d8,X 5 logic
EA ORAB d8,X 5 logic
EB ADDB d8,X 5 arithmetic
EE LDX d8,X 6 load
EF STX d8,X 7 load
F0 SUBB a16 4 arithmetic
F1 CMPB a16 4 compare
F2 SBCB a16 4 arithmetic
F4 ANDB a16 4 logic
F5 BITB a16 4 logic
F6 LDAB a16 4 load
F7 STAB a16 5 load
F8 EORB a16 4 logic
FA ORAB a16 4 logic
FB ADDB a16 4 arithmetic
FC ADCA a16 4 arithmetic
FE LDX a16 5 load
FF STX a16 6 load
89 ADCA #d8 2 arithmetic
99 ADCA d8 3 arithmetic
A9 ADCA d8,X 5 arithmetic
B9 ADCA a16 4 arithmetic
C9 ADCB #d8 2 arithmetic
D9 ADCB d8 3 arithmetic
E9 ADCB d8,X 5 arithmetic
F9 ADCB a16 4 arithmetic
20 BRA rel 4 jump
22 BHI rel 4 jump
23 BLS rel 4 jump
24 BCC rel 4 jump
25 BCS rel 4 jump
26 BNE rel 4 jump
27 BEQ rel 4 jump
28 BVC rel 4 jump
29 BVS rel 4 jump
2A BPL rel 4 jump
2B BMI rel 4 jump
2C BGE rel 4 jump
2D BLT rel 4 jump
2E BGT rel 4 jump
2F BLE rel 4 jump
""".strip().split("\n")


def words_of(template):
    if template.endswith(("#d16", "a16")) or "#d16" in template:
        return 3
    if template.endswith(("#d8", "d8", "d8,X", "rel")) or any(
        template.endswith(s) for s in ("#d8", "d8", "d8,X", "rel")
    ):
        return 2
    return 1


def main():
    instructions = []
    for row in ROWS:
        parts = row.split()
        opcode, cycles, group, template = (
            parts[0],
            int(parts[-2]),
            parts[-1],
            " ".join(parts[1:-2]),
        )
        words = (
            3
            if "#d16" in template or template.endswith("a16")
            else 2
            if any(template.endswith(s) for s in ("#d8", "d8", "d8,X", "rel"))
            else 1
        )
        instructions.append(
            {
                "opcode": opcode,
                "mnemonic": template,
                "cycles": cycles,
                "words": words,
                "semantics": None,
                "group": group,
                "encoding": opcode,
                "operands": None,
                "variants": None,
            }
        )
    OUT.write_text(
        json.dumps(
            {
                "$schema": "isa.schema.json",
                "processor": "M6800",
                "format_version": "1.0",
                "description": "Motorola 6800 (udokumentowane instrukcje).",
                "instructions": instructions,
            },
            indent=2,
        )
        + "\n"
    )
    print(f"6800: {len(instructions)} instructions")


if __name__ == "__main__":
    main()
