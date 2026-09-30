
# Fortran implementation plan

Status: **design proposal** — implementation not started.

Related:
- [ir-vreg-plan.md](ir-vreg-plan.md)
- [minic-optimization.md](minic-optimization.md)
- [targets.md](targets.md)

## 1. Goal

Add a Fortran frontend to CathodeRay without turning VReg into a Fortran-specific IR.

Architecture:

~~~text
Fortran source
  → source-form normalization
  → preprocessing / INCLUDE handling
  → lexer
  → parser
  → semantic analysis
  → Fortran semantic IR
  → array / descriptor / CHARACTER lowering
  → VReg IR
  → VReg optimization
  → SSA (later)
  → register allocation
  → target emission
~~~

The frontend must preserve Fortran semantics until they can be lowered safely. In particular, multidimensional arrays, non-default bounds, assumed/deferred shape, allocatable/pointer variables, CHARACTER values, derived types and procedure argument semantics must not be prematurely represented as plain C-like pointers.

GNU Fortran documents that assumed-shape/rank and deferred-rank arrays use descriptors, while ordinary explicit-size arrays can use simpler address representations. It also documents hidden character-length and optional-argument information in procedure calling conventions. These are useful design references, but CathodeRay must define its own target ABI.

## 2. Initial language scope

Do not start with all of modern Fortran.

### Phase F1 — Mini-Fortran

Support:

- free-form source
- comments and continuation
- PROGRAM
- SUBROUTINE
- FUNCTION
- IMPLICIT NONE
- INTEGER
- REAL
- LOGICAL
- CHARACTER with fixed length
- PARAMETER
- DIMENSION
- explicit-shape arrays
- scalar expressions
- array element access
- assignment
- IF / ELSE IF / ELSE
- DO
- EXIT / CYCLE
- RETURN
- CALL
- basic intrinsic functions
- relational and logical operators
- simple formatted/unformatted runtime I/O
- MODULE / USE only when required by the implementation architecture

### Phase F2 — useful Fortran

Add:

- derived types
- assumed-shape arrays
- assumed-size arrays
- allocatable arrays
- ALLOCATE / DEALLOCATE
- OPTIONAL / INTENT
- array sections
- elemental operations
- SELECT CASE
- WHERE
- internal procedures
- modules and module variables
- SIZE, SHAPE, LBOUND, UBOUND, SUM, MINVAL, MAXVAL

### Phase F3 — legacy compatibility

Add selectively:

- fixed-form source
- FORMAT
- DATA
- COMMON
- EQUIVALENCE
- computed GOTO / assigned GOTO if required
- legacy type declarations
- Hollerith only as an explicit compatibility extension

### Phase F4 — advanced modern Fortran

Potentially add:

- pointers
- polymorphic/class types
- type-bound procedures
- generic interfaces
- deferred-length CHARACTER
- assumed-rank
- coarrays
- procedure pointers
- asynchronous I/O
- finalization

F4 is not a prerequisite for a useful compiler.

## 3. Source forms

Fortran has two major source forms.

### Free form

Primary implementation target:

~~~fortran
program hello
  implicit none
  integer :: i

  do i = 1, 10
    print *, i
  end do
end program hello
~~~

### Fixed form

Support later:

~~~text
      PROGRAM HELLO
      INTEGER I
      DO 10 I = 1,10
      PRINT *, I
   10 CONTINUE
      END
~~~

Do not put fixed-form column rules into the core lexer. Normalize source first:

~~~text
fixed-form source
        ↓
logical Fortran source lines
        ↓
common lexer
~~~

This keeps the parser independent of source layout.

## 4. Frontend architecture

Suggested layout:

~~~text
src/CathodeRay.C/
  Fortran/
    Lexing/
      FortranLexer.cs
      FortranToken.cs
      FortranSourceNormalizer.cs
    Parsing/
      FortranParser.cs
      FortranSyntax.cs
    Semantics/
      FortranType.cs
      FortranSymbol.cs
      FortranScope.cs
      FortranSemanticAnalyzer.cs
      FortranModule.cs
    IR/
      FortranIr.cs
      FortranArray.cs
      FortranProcedure.cs
      FortranLowering.cs
    Runtime/
      FortranRuntime.cs
      FortranIoRuntime.cs
      FortranDecimalRuntime.cs
~~~

Keep the existing Mini-C frontend independent.

Shared infrastructure may include diagnostics, source spans, symbol IDs, constant evaluation, CFG utilities, target/ABI abstractions and VReg backend.

Do not share a parser merely because both languages have IF, DO and functions.

## 5. Fortran semantic type system

Fortran type identity is not equivalent to C type identity.

Minimum model:

~~~text
FortranType
  ├─ Integer(kind)
  ├─ Real(kind)
  ├─ Complex(kind)
  ├─ Logical(kind)
  ├─ Character(kind, length)
  ├─ DerivedType(name, components)
  ├─ Array(elementType, rank, bounds)
  └─ Procedure(...)
~~~

Later:

~~~text
  ├─ Class(type)
  ├─ Pointer(type)
  ├─ Allocatable(type)
  └─ AssumedType / AssumedRank
~~~

The distinction between type, kind, rank, bounds and storage attributes must remain explicit.

Fortran arrays are normally 1-based by default, multidimensional and column-major. C interoperability documentation explicitly describes the dimension-order and lower-bound differences.

## 6. Array representation

This is the most important backend-facing design decision.

### 6.1 Explicit-shape array

Example:

~~~fortran
integer :: a(10, 20)
x = a(i, j)
~~~

Semantic representation:

~~~text
ArrayValue
  data = address(a)
  rank = 2
  bounds = [(1,10), (1,20)]
  strides = [1,10]
~~~

Element address:

~~~text
offset = ((i-lb1) + (j-lb2)*extent1) * elementSize
~~~

Constant extents and strides should be folded whenever possible.

### 6.2 Assumed-shape / dynamic array

Use an explicit descriptor:

~~~text
FortranArrayDescriptor
  data
  rank
  elementSize
  dimension[rank]
    lowerBound
    extent
    stride
~~~

For a first implementation, a statically sized descriptor with a small maximum rank is acceptable.

### 6.3 Array sections

Example:

~~~fortran
b = a(2:10:2)
~~~

Do not immediately copy the section.

Represent it as a view:

~~~text
ArrayView
  base
  lowerBound
  extent
  stride
~~~

Only materialize a temporary when required by semantics, argument association or runtime ABI.

### 6.4 Array intrinsics

Initially lower operations such as SIZE, SHAPE, LBOUND, UBOUND and SUM to runtime helpers or explicit loops. Later, recognize them as optimization candidates.

## 7. CHARACTER

CHARACTER must not be represented as a C NUL-terminated string by default.

Minimum semantic representation:

~~~text
CharacterValue
  address
  length
  kind
~~~

For fixed-length locals, length may be compile-time constant.

For procedure arguments, length may be dynamic. This must be represented explicitly in the Fortran procedure model rather than relying on accidental VReg calling conventions.

Required initial operations:

- assignment with padding/truncation
- substring
- concatenation
- comparison
- LEN
- indexing
- simple formatted output

Later:

- deferred length
- CHARACTER arrays
- intrinsic character functions
- C interoperability

## 8. Derived types

Example:

~~~fortran
type :: Customer
  integer :: id
  real :: balance
end type Customer
~~~

Semantic representation:

~~~text
DerivedType
  name
  fields:
    name
    type
    offset
    alignment
~~~

Lowering:

~~~text
%customer = local Customer
%balanceAddr = field_addr %customer, offset(balance)
%balance = load real, %balanceAddr
~~~

VReg therefore needs an explicit aggregate/address model rather than language-specific field instructions.

C-interoperable BIND(C) types must be treated as an explicit layout contract, not as an assumption about ordinary derived types.

## 9. Procedures and ABI

Fortran procedure declarations must remain language-level objects until ABI lowering.

Model:

~~~text
FortranProcedure
  name
  parameters[]
  result
  attributes
  callingConvention
~~~

Parameter attributes:

- INTENT(IN)
- INTENT(OUT)
- INTENT(INOUT)
- VALUE
- OPTIONAL
- array shape information
- CHARACTER length
- pointer/allocatable status

Then:

~~~text
Fortran ABI
    ↓
VReg call signature
    ↓
target ABI
~~~

Do not make Fortran calls use the C ABI by default.

Provide explicit interoperability later through BIND(C).

## 10. Control flow

Build explicit basic blocks during lowering.

Example:

~~~fortran
if (x > 0) then
  y = 1
else
  y = 2
end if
~~~

becomes:

~~~text
B0
 ├─ cmp
 └─ br B1, B2

B1
 └─ y = 1
     jmp B3

B2
 └─ y = 2
     jmp B3

B3
~~~

This directly supports the existing VReg design and future SSA construction.

DO loops must preserve initialization, limit, increment, termination, EXIT and CYCLE semantics.

## 11. Intrinsics

Create a registry rather than hard-coding every intrinsic in the parser.

~~~text
IFortranIntrinsic
  Name
  Type inference
  Semantic validation
  Lowering
~~~

Initial categories:

- arithmetic: ABS, MOD, SIGN
- inquiry: SIZE, SHAPE, LBOUND, UBOUND
- character: LEN, TRIM, INDEX
- conversion: INT, REAL, NINT
- math: SQRT, SIN, COS, EXP, LOG
- array reduction: SUM, MINVAL, MAXVAL

Each intrinsic should have semantic validation, constant folding where possible, VReg lowering and a runtime fallback.

## 12. I/O runtime

Do not implement full Fortran I/O in the compiler initially.

Use runtime calls:

~~~text
PRINT / WRITE
   ↓
FortranIoRuntime
   ↓
target/runtime implementation
~~~

Start with stdout, stdin, integer, real, character and newline.

Add files, FORMAT, OPEN/CLOSE/READ/WRITE later.

## 13. VReg extensions required by Fortran

The current VReg design is close, but the following should be explicit:

~~~text
Typed value
Address
Aggregate address
Load/store
Field address
Element address
Call
Return
CFG
Stack slot
Memory side-effect marker (later)
~~~

Recommended conceptual operations:

~~~text
%p = field_addr %base, offset
%p = elem_addr %array, %index
%v = load type, %p
store type, %p, %v
%v = call ...
~~~

Do not add Fortran-specific instructions such as fortran_array_add or fortran_do_loop to VReg. They belong to the Fortran semantic/lowering layer.

## 14. Optimization strategy

Fortran-specific optimizations should happen before generic VReg lowering when semantic information is available.

~~~text
Fortran array expression
        ↓
array-aware optimization
        ↓
element loop / runtime call
        ↓
VReg
        ↓
generic optimization
~~~

Generic VReg passes can perform constant folding, dead vreg elimination, local CSE, CFG simplification and later SSA/GVN/LICM.

Array-specific optimizations can later include loop-invariant bounds, stride simplification, temporary elimination, scalar replacement, intrinsic expansion and simple loop fusion.

Do not attempt aggressive automatic vectorization in the first implementation.

## 15. Runtime model

Initial runtime modules:

~~~text
FortranRuntime
  ├─ allocation
  ├─ bounds helpers
  ├─ character helpers
  ├─ numeric conversion
  └─ error/stop

FortranIoRuntime
  ├─ print
  ├─ read
  ├─ write
  └─ newline
~~~

Runtime semantics should be target-independent, with target-specific implementations where required.

For 8-bit targets, prefer compact helper routines over large inline sequences.

## 16. Diagnostics

Use Fortran terminology:

- undeclared variable
- invalid kind
- rank mismatch
- shape mismatch
- invalid array bound
- invalid INTENT usage
- missing END IF
- missing END DO
- invalid procedure argument
- invalid CHARACTER length
- invalid intrinsic argument

All diagnostics must retain source spans after fixed/free-form normalization.

## 17. Testing

Use four layers.

### Parser tests

- free form
- fixed form
- continuation
- comments
- case insensitivity
- keywords

### Semantic tests

- implicit typing
- IMPLICIT NONE
- kinds
- ranks
- bounds
- argument checking
- derived types
- CHARACTER

### IR tests

- scalar lowering
- array addressing
- descriptor lowering
- procedure calls
- CFG
- runtime calls

### End-to-end tests

~~~text
Fortran source
  → Fortran IR
  → VReg
  → interpreter
  → target code
~~~

Required differential tests:

~~~text
Fortran semantic execution
        ==
VRegInterpreter
        ==
allocated VReg execution
        ==
hardware/emulator execution
~~~

## 18. CLI

Recommended:

~~~bash
cathode cc hello.f90 -o hello.bin --cpu z80 --lang fortran
cathode cc hello.f90 -o hello.bin --cpu stub --lang fortran --ir vreg
cathode cc hello.f -o hello.bin --lang fortran --fixed-form
~~~

Language detection may later use extensions:

~~~text
.c       → C
.f       → Fortran fixed form
.for     → Fortran fixed form
.f90     → Fortran free form
.f95     → Fortran free form
.f03     → Fortran free form
.f08     → Fortran free form
~~~

Keep --lang explicit for ambiguous or generated sources.

## 19. Migration phases

| Phase | Work | Exit criteria |
| --- | --- | --- |
| F0 | source normalization + tokens | lexer tests pass |
| F1 | parser + scalar semantic model | simple PROGRAM compiles |
| F2 | scalar lowering to VReg | arithmetic/control-flow tests pass |
| F3 | explicit-shape arrays | multidimensional array tests pass |
| F4 | procedures + ABI | SUBROUTINE/FUNCTION tests pass |
| F5 | CHARACTER + basic I/O runtime | text programs run |
| F6 | derived types | field access/layout tests pass |
| F7 | assumed-shape descriptors | descriptor tests pass |
| F8 | allocatable + dynamic arrays | allocation tests pass |
| F9 | array sections/intrinsics | semantic suite passes |
| F10 | modules / useful legacy features | multi-file tests pass |
| F11 | optimization | measured code-size/cycle improvements |
| F12 | advanced Fortran | only features justified by use cases |

## 20. Recommended first milestone

Compile:

~~~fortran
program demo
  implicit none
  integer :: i
  integer :: a(10)

  do i = 1, 10
    a(i) = i * 2
  end do

  print *, a(5)
end program demo
~~~

This forces the architecture to solve lexical rules, declarations, scalar types, arrays, 1-based indexing, memory operations, loops, runtime I/O and VReg CFG without immediately requiring dynamic descriptors.

## 21. Definition of done

1. Mini-Fortran parses without language-specific hacks in VReg.
2. Scalar and explicit-shape array programs lower to VReg.
3. VRegInterpreter produces correct results.
4. At least one simple target executes generated code.
5. Array indexing is correct for supported arbitrary lower bounds.
6. Procedure calls have an explicit Fortran ABI lowering stage.
7. CHARACTER is length-aware.
8. Fortran-specific semantics do not leak into generic register allocation.
9. Existing Mini-C and Cell IR tests remain unchanged and passing.
10. Adding a new Fortran construct does not require modifying the VReg allocator.

## 22. Main risks

| Risk | Mitigation |
| --- | --- |
| Array semantics become VReg-specific | dedicated Fortran IR + lowering |
| CHARACTER leaks into backend | explicit address+length representation |
| ABI becomes accidental | dedicated Fortran ABI lowering |
| Full standard scope explodes | staged language subsets |
| 8-bit code becomes huge | runtime helpers + measurement |
| Fixed form contaminates parser | source normalization |
| Legacy constructs block progress | isolate them in compatibility phases |

> **Fortran semantics belong above VReg; machine/resource concerns belong below VReg.**
