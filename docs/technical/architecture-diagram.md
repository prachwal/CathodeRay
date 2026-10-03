# Architektura kompilatora — szkic graficzny

Opis słowny: [architecture.md](architecture.md). Status: [mini-c-status.md](mini-c-status.md).

## 1. Potok kompilacji (`cathode cc`)

```mermaid
flowchart TD
    SRC["plik.c"] --> PRE["CPreprocessor"]
    PRE --> LEX["Lexer"]
    LEX --> PAR["Parser"]
    PAR --> TCK["TypeChecker"]
    TCK --> CHK["CheckedProgram"]

    CHK --> LOW["Lowering<br/>(per funkcja: Wide8Legalizer → IrPasses<br/>stałe, kopie, DCE)"]
    LOW --> INL["IrInliner<br/>(funkcje liście)"]
    INL --> IRCELL["Cell IR<br/>(komórki absolutne W=1/2/4)"]

    IRCELL --> CF["CaseFold"]
    CF --> L1["Legalizer wide<br/>(mnożenie/dzielenie → __cc_*)"]
    L1 --> WL["WideLegalizer<br/>(W=4 → połówki 16-bit)"]
    WL --> L2["Legalizer narrow"]
    L2 --> ZF["IndexFusion + ZeroPage<br/>(tylko wybrane cele)"]
    ZF --> BSEL["ByteSelector"]
    BSEL --> ISA["XxxIsa : ByteIsa<br/>(jedyna część zależna od CPU)"]
    ISA --> PH["Peephole / BranchRelaxer"]
    PH --> ASM["tekst asemblera"]

    ASM --> ASMB["asembler<br/>(TwoPassAssembler)"]
    CRT["crt0 celu"] --> LINK["linker<br/>(crt0 pierwszy, stdlib na żądanie)"]
    ASMB --> LINK
    STDLIB["stdlib<br/>(portable C + target .s)"] -.-> LINK
    LINK --> BIN["program.bin"]
```

Ścieżka `--ir vreg` ([VRegPipeline.cs](../../src/CathodeRay.C/VReg/VRegPipeline.cs)) nie jest osobnym frontem:
owija `Codegen.Lower` i wraca do tego samego Cell IR (cały dalszy potok bez zmian).

```mermaid
flowchart LR
    IRCELL["Cell IR<br/>(z Codegen.Lower)"] --> LIFT["VRegLift"]
    LIFT --> VPASS["VRegPasses<br/>(CSE, DCE)"]
    VPASS --> V2C["VRegToCell"]
    V2C --> OUT["Cell IR<br/>→ CaseFold … selektor"]
    VPASS -.->|"keep-set, tylko doradczy"| ALLOC["Allocator<br/>(accumulator / linear-scan)"]
```

## 2. Moduły repozytorium

```mermaid
flowchart LR
    subgraph SRCF["src/"]
        CA["CathodeRay.Assembler<br/>(asembler + linker)"]
        CC["CathodeRay.C<br/>(frontend, IR, cele, stdlib)"]
        CLI["CathodeRay.Cli<br/>(asm | cc | link | stub)"]
        CORE["CathodeRay<br/>(rdzeń, emulator stub)"]
    end
    subgraph SUPP["wsparcie"]
        TST["tests/<br/>(emulatory CPU, wyrocznie, goldeny)"]
        SAMP["samples/<br/>(minic, bench)"]
        DATA["data/<br/>(ISA JSON)"]
        TOOLS["tools/<br/>(compare, fuzz, goldeny)"]
    end
    CLI --> CC
    CLI --> CA
    TST --> CC
    TST --> CA
```

Zależności jednokierunkowe: frontend C nie zależy od asemblera, asembler nie zależy od emulatorów.

## 3. ABI v1 — przepływ argumentów i wyniku (cele bajtowe)

```mermaid
flowchart LR
    CALLER["wołający"]
    CALLEE["wołany"]

    CALLER -->|"arg1: A/X (stub)<br/>arg2..6: cc_argN (+_h)"| CALLEE
    CALLEE -->|"prolog: cc_argN → komórki<br/>(ParamAlias: alias liścia)"| CALLEE
    CALLEE -->|"wynik: cc_ret (+_h)<br/>Z80/8080: HL (plan 35)<br/>struct/long long: cc_retbuf"| CALLER
    CRT0["crt0"] -.->|"po call main:<br/>HL → cc_ret (Z80/8080)"| CALLER
```

Wyjątki: `long` dokłada `cc_rethi`, `long long`/struktury jadą przez adres/bufor.
ABI v2 (argumenty w rejestrach, pilot `nes`, flaga `--abi`): [../ir-vreg-abi/abi-v2-design.md](../ir-vreg-abi/abi-v2-design.md).
