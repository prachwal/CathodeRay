
# COBOL implementation plan

Status: **design proposal** — implementation not started.

Related:
- [ir-vreg-plan.md](ir-vreg-plan.md)
- [minic-optimization.md](minic-optimization.md)
- [targets.md](targets.md)

## 1. Goal

Add a COBOL frontend to CathodeRay using a COBOL-specific semantic IR and the shared VReg backend.

Architecture:

~~~text
COBOL source
  → source normalization
  → lexer
  → parser
  → DATA DIVISION semantic model
  → PROCEDURE DIVISION semantic model
  → COBOL semantic IR
  → data-layout / decimal / string lowering
  → VReg IR
  → optimization
  → register allocation
  → target emission
~~~

COBOL should not be translated directly into ad-hoc VReg instructions. The language has a strong data-description model, hierarchical records, PIC clauses, USAGE, decimal arithmetic, file I/O and paragraph/section control flow. These semantics need to survive long enough to be validated and lowered correctly.

GnuCOBOL documents PIC as defining the class, size and format of a data item, with USAGE also participating in representation. This differs fundamentally from treating every COBOL field as a normal C-like integer or string.

## 2. Initial scope

Do not target all historical COBOL dialects initially.

### Phase C1 — Mini-COBOL

Support:

- source normalization
- IDENTIFICATION DIVISION
- ENVIRONMENT DIVISION in minimal form
- DATA DIVISION
- WORKING-STORAGE SECTION
- LINKAGE SECTION
- PROCEDURE DIVISION
- elementary data items
- group items
- PIC X
- PIC 9
- PIC S9
- COMP / binary integer
- fixed-size alphanumeric fields
- VALUE
- MOVE
- ADD
- SUBTRACT
- MULTIPLY
- DIVIDE
- COMPUTE
- IF / ELSE
- EVALUATE
- PERFORM
- GO TO
- STOP RUN
- CALL / CANCEL in a restricted form
- DISPLAY
- ACCEPT
- simple sequential file support later

### Phase C2 — business-data COBOL

Add:

- OCCURS
- REDEFINES
- RENAMES
- edited PIC fields
- COMP-3 packed decimal
- decimal arithmetic
- reference modification
- INSPECT
- STRING / UNSTRING
- INITIALIZE
- SET
- SEARCH
- file SELECT / FD
- OPEN / CLOSE / READ / WRITE

### Phase C3 — enterprise COBOL

Add selectively:

- COPY
- EXEC SQL integration boundary
- CALL conventions
- external programs
- indexed / relative files
- report writer
- declaratives
- SORT / MERGE
- screen I/O

### C4 — dialect compatibility

Only when required:

- IBM Enterprise COBOL extensions
- Micro Focus extensions
- GnuCOBOL extensions
- compiler directives

Keep dialect extensions out of the core semantic model where possible.

## 3. Source normalization

COBOL source traditionally uses fixed columns and sequence areas, but modern dialects also support free-form constructs.

Normalize before lexing:

~~~text
COBOL source
   ↓
source-form normalizer
   ↓
logical lines / tokens
   ↓
common COBOL lexer
~~~

Handle:

- sequence numbers
- identification area
- indicator area
- continuation
- comments
- compiler directives
- case-insensitive keywords

The lexer should not need to know that a token occupied a particular physical column.

## 4. Frontend architecture

Suggested layout:

~~~text
src/CathodeRay.C/
  Cobol/
    Lexing/
      CobolLexer.cs
      CobolToken.cs
      CobolSourceNormalizer.cs
    Parsing/
      CobolParser.cs
      CobolSyntax.cs
    Data/
      CobolDataDescription.cs
      CobolPic.cs
      CobolUsage.cs
      CobolDataLayout.cs
    Semantics/
      CobolType.cs
      CobolSymbol.cs
      CobolScope.cs
      CobolSemanticAnalyzer.cs
    IR/
      CobolIr.cs
      CobolLowering.cs
      CobolControlFlow.cs
    Runtime/
      CobolRuntime.cs
      CobolDecimalRuntime.cs
      CobolIoRuntime.cs
~~~

The DATA DIVISION should have a first-class model. Do not flatten it immediately into ordinary local variables.

## 5. Data description hierarchy

This is the central COBOL-specific structure.

Example:

~~~cobol
01 CUSTOMER-RECORD.
   05 CUSTOMER-ID      PIC 9(8).
   05 CUSTOMER-NAME    PIC X(40).
   05 BALANCE          PIC S9(7)V99 COMP-3.
~~~

Represent:

~~~text
GroupItem CUSTOMER-RECORD
  ├─ ElementaryItem CUSTOMER-ID
  ├─ ElementaryItem CUSTOMER-NAME
  └─ ElementaryItem BALANCE
~~~

Every data item should have:

~~~text
CobolDataItem
  level
  name
  parent
  children[]
  picture?
  usage
  occurs?
  redefines?
  value?
  offset
  size
  alignment
  category
~~~

This permits the compiler to answer what the field's type is, where it is stored, its size, numeric category, addressability, overlap and array semantics.

## 6. PIC model

PIC must be parsed into a structured representation.

Example:

~~~text
PIC S9(7)V99
~~~

becomes:

~~~text
CobolPic
  sign = Signed
  integerDigits = 7
  fractionalDigits = 2
  scaling = 0
  category = Numeric
~~~

For PIC X(40):

~~~text
category = Alphanumeric
length = 40
~~~

For edited output:

~~~text
PIC $$$,$$9.99
~~~

preserve the editing mask.

Do not make PIC parsing part of VReg.

## 7. COBOL categories and storage

The semantic model should distinguish:

~~~text
DISPLAY numeric
DISPLAY alphanumeric
binary integer
packed decimal
floating point (later)
edited numeric
national / Unicode-related types (later)
~~~

Separation:

~~~text
logical category
       ↓
COBOL storage representation
       ↓
VReg representation
~~~

This prevents a DISPLAY numeric field from being treated as a native integer.

## 8. COMP and COMP-3

### COMP / binary

Represent as a typed integer with an explicit storage width.

Example:

~~~text
PIC S9(9) COMP
~~~

may lower to a target integer representation.

Exact storage size must be controlled by the selected COBOL dialect/ABI, not inferred solely from mathematical range.

### COMP-3 / packed decimal

Packed decimal needs a dedicated semantic/runtime representation.

Example:

~~~text
PIC S9(7)V99 COMP-3
~~~

Conceptually:

~~~text
DecimalValue
  sign
  precision = 9
  scale = 2
  representation = PackedDecimal
~~~

VReg should not acquire a comp3_add instruction.

Instead:

~~~text
COBOL decimal operation
        ↓
decimal lowering
        ↓
runtime/helper or primitive integer sequence
        ↓
VReg
~~~

On 8-bit targets, a decimal runtime helper may be much smaller than inline arithmetic.

## 9. Group items and REDEFINES

Group items map naturally to aggregates:

~~~text
CustomerRecord
  field offsets
  total size
~~~

But REDEFINES means two semantic views can overlap the same storage.

Example:

~~~cobol
01 RAW-DATA PIC X(10).
01 RAW-VIEW REDEFINES RAW-DATA.
   05 CODE PIC 9(4).
   05 VALUE PIC 9(6).
~~~

Do not model this as two independent VRegs.

Both views must resolve to the same address.

Recommended lowering:

~~~text
%base = address RAW-DATA
%codeAddr = add %base, 0
%valueAddr = add %base, 4
~~~

This is one reason VReg needs a robust address/memory model.

## 10. OCCURS

Example:

~~~cobol
05 ITEM PIC 9(5) OCCURS 20 TIMES.
~~~

Semantic model:

~~~text
Array
  element = ITEM
  count = 20
  stride = sizeof(ITEM)
~~~

Indexed access:

~~~text
%offset = mul %index, stride
%addr = add %base, %offset
%value = load type, %addr
~~~

For nested OCCURS, preserve each dimension.

Do not convert COBOL OCCURS into a language-specific VReg instruction.

## 11. Strings

COBOL strings are fixed-width data items much more often than C-style NUL-terminated strings.

For PIC X(40):

~~~text
address + length
~~~

There is no implicit NUL terminator.

Required operations:

- MOVE with padding/truncation
- comparison
- reference modification
- STRING
- UNSTRING
- INSPECT
- DISPLAY

Runtime helpers should be used where generated inline code would be excessive.

## 12. Reference modification

Example:

~~~cobol
MOVE CUSTOMER-NAME(1:10) TO SHORT-NAME.
~~~

Lower to a view:

~~~text
%src = address CUSTOMER-NAME
%sub = string_view %src, offset=0, length=10
call cobol_move %sub, SHORT-NAME
~~~

The semantic layer validates starting position, length, bounds and target type.

VReg only receives address/length operations.

## 13. Arithmetic

COBOL arithmetic must be decimal-aware.

Operations:

- ADD
- SUBTRACT
- MULTIPLY
- DIVIDE
- COMPUTE
- ROUNDED
- ON SIZE ERROR

Represent a semantic arithmetic operation as:

~~~text
DecimalOp
  operands
  result
  scale
  rounding
  sizeErrorTarget?
~~~

Lower later.

For native binary integers, ordinary VReg integer operations are sufficient.

For packed/display decimal, use a decimal runtime or specialized lowering pass.

## 14. MOVE semantics

MOVE is not equivalent to raw memory copy.

Examples include:

- numeric → numeric
- numeric → edited numeric
- alphanumeric → alphanumeric
- group → group
- elementary → group

The semantic checker determines the category conversion first.

Lowering may produce:

~~~text
memcpy
numeric conversion
decimal conversion
formatting
padding
truncation
~~~

depending on source and target types.

## 15. PROCEDURE DIVISION

COBOL control flow is naturally paragraph/section oriented.

Example:

~~~cobol
MAIN-PARA.
    PERFORM CALC-PARA
    DISPLAY TOTAL
    STOP RUN.

CALC-PARA.
    COMPUTE TOTAL = PRICE * QUANTITY.
~~~

Build explicit CFG nodes:

~~~text
Program
  ├─ paragraph MAIN-PARA
  └─ paragraph CALC-PARA
~~~

Then lower to ordinary basic blocks.

The backend should not know what a COBOL paragraph is.

## 16. PERFORM

Support forms separately.

### Simple procedure

~~~cobol
PERFORM CALC-PARA
~~~

Lower as a call-like control-flow edge where possible.

### Range

~~~cobol
PERFORM PARA-A THRU PARA-C
~~~

Resolve the paragraph range during semantic analysis and construct explicit CFG edges.

### Times

~~~cobol
PERFORM 10 TIMES
~~~

Lower to a counted loop.

### UNTIL

~~~cobol
PERFORM UNTIL DONE
~~~

Lower to a normal CFG loop.

## 17. GO TO and legacy control flow

Unstructured jumps should be represented as explicit CFG edges.

Legacy constructs such as ALTER should be isolated behind a compatibility layer because they complicate CFG reasoning and optimization.

If supported, aggressive CFG optimizations should be disabled around dynamically altered control flow.

## 18. Conditions

COBOL conditions include:

- relational comparisons
- numeric comparisons
- alphanumeric comparisons
- class conditions
- NOT
- AND / OR
- abbreviated combined conditions

Normalize them into boolean semantic expressions before VReg lowering.

Example:

~~~text
IF BALANCE > 1000 AND CUSTOMER-STATUS = "A"
~~~

becomes:

~~~text
%a = cmp gt, %balance, 1000
%b = cmp eq, %status, "A"
%c = and, %a, %b
br %c, ...
~~~

## 19. File I/O

File I/O should be runtime-backed.

Initial abstraction:

~~~text
CobolFile
  organization = sequential
  recordLayout
  status
~~~

Statements:

- OPEN
- CLOSE
- READ
- WRITE

lower to CobolIoRuntime.

File status must be explicit so AT END, INVALID KEY and FILE STATUS can be lowered correctly.

Do not make filesystem semantics part of generic VReg.

## 20. Runtime

Suggested runtime:

~~~text
CobolRuntime
  ├─ memory
  ├─ decimal
  ├─ numeric conversion
  ├─ string
  ├─ formatting
  └─ error/status

CobolIoRuntime
  ├─ open
  ├─ close
  ├─ read
  ├─ write
  └─ display
~~~

The runtime interface should be target-independent. Target-specific implementations can be selected later.

## 21. VReg requirements

COBOL reinforces the same VReg requirements as Fortran.

Required generic operations:

~~~text
address
field_addr
element_addr
load
store
memcpy/copyblock
fill
call
ret
cmp
br
jmp
~~~

Recommended typed model:

~~~text
VRegType
  ├─ Integer(width, signed)
  ├─ Float(width)
  ├─ Pointer
  ├─ Address
  ├─ Aggregate
  └─ ByteSequence / opaque memory
~~~

Decimal and fixed-format COBOL values should normally be lowered before generic VReg code generation.

## 22. COBOL semantic IR

A useful intermediate model is:

~~~text
CobolModule
  programs[]
  dataDescriptions[]
  procedures[]
  files[]
~~~

Procedure operations:

~~~text
Move
Arithmetic
Compare
Call
Perform
Goto
If
Evaluate
Display
Accept
Read
Write
StopRun
~~~

This IR is intentionally not a backend IR. Its purpose is to make COBOL semantics explicit before lowering to CFG + VReg.

## 23. Optimization strategy

Optimize semantic COBOL operations before lowering only where the operation is clearly safe.

Examples:

- constant PIC values
- constant arithmetic
- fixed-size MOVE
- redundant conversion
- dead DISPLAY-independent calculations

After lowering, generic VReg passes handle constant folding, dead vreg elimination, local CSE and CFG simplification, followed later by SSA/GVN/LICM.

Do not perform unsafe alias analysis across REDEFINES or address-taken data.

## 24. CLI

Recommended:

~~~bash
cathode cc hello.cob -o hello.bin --lang cobol --cpu stub
cathode cc hello.cbl -o hello.bin --lang cobol --cpu 6502 --ir vreg
~~~

Extensions:

~~~text
.cob
.cbl
.cobol
~~~

Potential later dialect option:

~~~bash
--cobol-dialect gnucobol
--cobol-dialect ibm
--cobol-dialect microfocus
~~~

Do not expose dialect options until they correspond to implemented behavior.

## 25. Testing

### Data tests

- level numbers
- group hierarchy
- PIC parser
- USAGE
- VALUE
- offsets
- alignment
- REDEFINES
- OCCURS

### Semantic tests

- MOVE conversions
- decimal arithmetic
- comparisons
- conditions
- PERFORM
- GO TO
- file status

### IR tests

- data addresses
- field offsets
- array indexing
- overlapping REDEFINES
- decimal runtime calls
- CFG

### End-to-end

~~~text
COBOL
  → semantic IR
  → VReg
  → VRegInterpreter
  → allocated VReg
  → target/emulator
~~~

Required allocator tests should include values living across PERFORM calls, branches, loops, runtime calls and decimal helper calls.

## 26. Migration phases

| Phase | Work | Exit criteria |
| --- | --- | --- |
| C0 | source normalization + lexer | source-form tests pass |
| C1 | DATA DIVISION model + PIC parser | data layout tests pass |
| C2 | PROCEDURE DIVISION parser | simple procedural programs parse |
| C3 | scalar MOVE/arithmetic/IF | VReg programs execute |
| C4 | groups + field addressing | nested record tests pass |
| C5 | OCCURS | array tests pass |
| C6 | decimal / COMP-3 | decimal oracle passes |
| C7 | PERFORM / paragraphs / EVALUATE | CFG tests pass |
| C8 | strings + reference modification | string tests pass |
| C9 | runtime I/O | DISPLAY/ACCEPT tests pass |
| C10 | sequential files | READ/WRITE tests pass |
| C11 | REDEFINES / legacy semantics | aliasing tests pass |
| C12 | dialect compatibility | only required extensions |
| C13 | optimization | measured improvements |

## 27. Recommended first milestone

First program:

~~~cobol
       IDENTIFICATION DIVISION.
       PROGRAM-ID. DEMO.

       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01  PRICE      PIC 9(5)V99.
       01  QUANTITY   PIC 9(3).
       01  TOTAL      PIC 9(8)V99.

       PROCEDURE DIVISION.
           MOVE 1250 TO PRICE
           MOVE 3 TO QUANTITY
           COMPUTE TOTAL = PRICE * QUANTITY
           DISPLAY TOTAL
           STOP RUN.
~~~

Second milestone:

~~~cobol
01 CUSTOMER.
   05 CUSTOMER-ID PIC 9(8).
   05 CUSTOMER-NAME PIC X(40).
   05 BALANCE PIC S9(7)V99 COMP-3.
~~~

These force implementation of DATA DIVISION hierarchy, PIC, numeric representation, MOVE, COMPUTE, DISPLAY, aggregate layout, decimal representation and runtime calls.

## 28. Definition of done

1. COBOL source normalization is independent of semantic parsing.
2. DATA DIVISION is represented as a hierarchy.
3. PIC and USAGE are parsed into structured semantic data.
4. Group fields have deterministic offsets.
5. REDEFINES preserves overlapping storage.
6. OCCURS has explicit element layout.
7. MOVE follows COBOL conversion semantics.
8. Decimal arithmetic has a defined representation/runtime.
9. PROCEDURE DIVISION becomes ordinary CFG.
10. VReg contains no COBOL-specific instructions.
11. VRegInterpreter and target execution agree.
12. Existing Mini-C and Cell IR paths remain unaffected.
13. A new target does not require changes to the COBOL frontend.

## 29. Main risks

| Risk | Mitigation |
| --- | --- |
| PIC becomes a backend type system | keep PIC semantic until representation lowering |
| REDEFINES breaks optimization | explicit aliasing/address model |
| COMP-3 becomes huge inline code | runtime helper first |
| COBOL dialect differences | explicit dialect layer |
| Paragraph control flow complicates SSA | lower to CFG before SSA |
| String semantics are lost | address+length model |
| File I/O dominates compiler work | runtime boundary |
| Full COBOL scope explodes | staged subsets |

> **COBOL data semantics belong above VReg; VReg represents the resulting typed memory operations and control flow.**
