using CathodeRay.Assembler;
using CathodeRay.Assembler.Link;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, krok 7: kolejność bajtów relokacji zależy od CPU obiektu (6800 to big-endian), a <c>#&lt;sym</c> i
/// <c>#&gt;sym</c> relokują się jako młodszy i starszy bajt adresu.</summary>
public sealed class LinkerEndiannessTests
{
    private const string Config = "MEMORY { RAM: start=$1000, size=$1000, file=%O, fill=yes; }\nSEGMENTS { CODE: load=RAM; DATA: load=RAM; }\n";

    private static ObjectModule Obj(string cpu, string source)
    {
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        return new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax).AssembleObject(cpu, source, "m", _ => null);
    }

    private static AssemblyResult Link(params ObjectModule[] modules) =>
        Linker.Link([.. modules.Select((m, i) => ($"m{i}.o", m))], LinkerConfig.Parse(Config));

    [Fact]
    public void Word_Relocations_Are_Big_Endian_For_6800_And_Little_Endian_Otherwise()
    {
        foreach ((string cpu, byte[] expected) in new[] { ("6800", new byte[] { 0x10, 0x03 }), ("6502", new byte[] { 0x03, 0x10 }) })
        {
            ObjectModule user = Obj(cpu, ".segment \"CODE\"\nNOP\n.extern there\n.word there\n");
            ObjectModule lib = Obj(cpu, ".global there\n.segment \"CODE\"\nthere: NOP\n");

            Link(user, lib).Image[1..3].Should().Equal(expected, cpu);
        }
    }

    [Fact]
    public void Extended_Addressing_Instruction_Gets_A_Big_Endian_Address_On_6800()
    {
        ObjectModule main = Obj("6800", ".extern target\n.global start\n.segment \"CODE\"\nstart: JMP target\n");
        ObjectModule lib = Obj("6800", ".global target\n.segment \"CODE\"\nNOP\ntarget: NOP\n");

        AssemblyResult result = Link(main, lib);

        result.Image[..3].Should().Equal(0x7E, 0x10, 0x04);
    }

    [Fact]
    public void Low_And_High_Byte_Immediates_Relocate_For_External_And_Local_Symbols()
    {
        ObjectModule main = Obj("6502", ".extern far\n.global start\n.segment \"CODE\"\nstart: LDA #<far\nLDX #>far\nLDY #<near\nLDA #>near\nNOP\n.global near\nnear: NOP\n");
        ObjectModule lib = Obj("6502", ".global far\n.segment \"CODE\"\n.res 300\nfar: NOP\n");

        AssemblyResult result = Link(main, lib);

        int near = result.Symbols["near"];
        int far = result.Symbols["far"];
        far.Should().BeGreaterThan(0x1100);
        result.Image[..8].Should().Equal(0xA9, (byte)far, 0xA2, (byte)(far >> 8), 0xA0, (byte)near, 0xA9, (byte)(near >> 8));
    }

    [Fact]
    public void Half_Byte_Relocations_Keep_The_Addend()
    {
        ObjectModule main = Obj("6502", ".extern far\n.segment \"CODE\"\nLDA #<(far+$FF)\nLDA #>(far+$FF)\n");
        ObjectModule lib = Obj("6502", ".global far\n.segment \"CODE\"\n.res 16\nfar: NOP\n");

        AssemblyResult result = Link(main, lib);

        int far = result.Symbols["far"] + 0xFF;
        result.Image[..4].Should().Equal(0xA9, (byte)far, 0xA9, (byte)(far >> 8));
    }

    [Fact]
    public void Constant_Halves_Are_Evaluated_At_Assembly_Time()
    {
        ObjectModule main = Obj("6502", ".segment \"CODE\"\nLDA #<$1234\nLDA #>$1234\n");

        AssemblyResult result = Link(main);

        result.Image[..4].Should().Equal(0xA9, 0x34, 0xA9, 0x12);
    }

    [Fact]
    public void Object_Files_With_Half_Relocations_Round_Trip()
    {
        ObjectModule main = Obj("6502", ".extern far\n.segment \"CODE\"\nLDA #<far\n");
        ObjectModule restored = ObjectModule.FromJson(main.ToJson());

        restored.Relocations.Select(static r => r.Kind).Should().Equal(RelocKind.Lo8);
    }
}
