using CathodeRay.Assembler.Isa.Targets;
using CathodeRay.Assembler.Syntax;

namespace CathodeRay.Assembler;

/// <summary>Rejestr celów. Nowy CPU = moduł w <c>Isa/Targets</c> + wpis tutaj.</summary>
public static class AssemblerTargets
{
    /// <summary>Wszystkie cele.</summary>
    public static IReadOnlyList<AssemblerTarget> All { get; } =
    [
        Mos6502("6502", "MOS 6502 (udokumentowane instrukcje)", "mcp_6502_instructions.json", Mos6502Variant.Nmos),
        Mos6502("6502x", "MOS 6502 z nieudokumentowanymi instrukcjami (jak ca65 6502X)", "mcp_6502_instructions.json", Mos6502Variant.NmosIllegal),
        Mos6502("65c02", "WDC/Rockwell 65C02", "mcp_65c02_instructions.json", Mos6502Variant.Cmos),
        new("8080", "Intel 8080", "mcp_8080_instructions.json", Intel8080Set.Load, [SyntaxDialects.Intel]),
        new("6800", "Motorola 6800", "mcp_6800_instructions.json", M6800Set.Load, [SyntaxDialects.Motorola]) { Endianness = Isa.Endianness.Big },
        new("z80", "Zilog Z80 (udokumentowane instrukcje)", "mcp_z80_instructions.json", static json => Z80Set.Load(json, Z80Variant.Documented), [SyntaxDialects.Zilog]),
        new("z80u", "Zilog Z80 z nieudokumentowanymi instrukcjami (IXH/IXL, SLL, OUT (C),0)", "mcp_z80_instructions.json", static json => Z80Set.Load(json, Z80Variant.Undocumented), [SyntaxDialects.Zilog]),
        new("stub", "ISA zaślepki CathodeRay", "mcp_stub_instructions.json", StubSet.Load, [SyntaxDialects.Stub]),
    ];

    /// <summary>Szuka celu po nazwie.</summary>
    /// <param name="name">Nazwa (<c>--cpu</c>).</param>
    /// <returns>Cel lub <see langword="null"/>.</returns>
    public static AssemblerTarget? Find(string name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    private static AssemblerTarget Mos6502(string name, string description, string isaFile, Mos6502Variant variant) =>
        new(name, description, isaFile, json => Mos6502Set.Load(json, variant), [SyntaxDialects.Ca65, SyntaxDialects.Mos]);
}
