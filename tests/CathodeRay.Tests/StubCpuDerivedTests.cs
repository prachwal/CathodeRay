using System.Text;
using CathodeRay.Abstractions;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class StubCpuDerivedTests
{
    private sealed class DerivedCpu : StubCpu
    {
        public DerivedCpu(StubIsa isa, IBus bus)
            : base(isa, bus)
        {
        }

        protected override void ConfigureOpcodes(StubIsa isa, StubOpcodeTable table)
        {
            base.ConfigureOpcodes(isa, table);
            table.Remove(0x00);
            table.Replace(0x01, new StubOpcodeEntry(DoubleLdi, "LDI", 1, 2));
            RegisterOpcode(table, 0x07, "SUB", 2, 2, Sub);
        }

        protected override Action<OpcodeContext>? ResolveBehavior(string mnemonic) =>
            mnemonic == "SUB" ? Sub : base.ResolveBehavior(mnemonic);

        private void DoubleLdi(OpcodeContext ctx) => State.A = (byte)(ctx.Operand * 2);

        private void Sub(OpcodeContext ctx) => State.A = (byte)(State.A - ctx.Operand);
    }

    private sealed class SubCpu : StubCpu
    {
        public SubCpu(StubIsa isa, IBus bus)
            : base(isa, bus)
        {
        }

        protected override Action<OpcodeContext>? ResolveBehavior(string mnemonic) =>
            mnemonic == "SUB" ? Sub : base.ResolveBehavior(mnemonic);

        private void Sub(OpcodeContext ctx) => State.A = (byte)(State.A - ctx.Operand);
    }

    private static StubIsa StubIsaFile()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "data", "instructions", "mcp_stub_instructions.json")))
        {
            dir = dir.Parent;
        }

        return StubIsa.FromJsonFile(Path.Combine(dir!.FullName, "data", "instructions", "mcp_stub_instructions.json"));
    }

    private static (StubCpu Cpu, StubBus Bus) Derived(params byte[] program)
    {
        var bus = new StubBus();
        for (int i = 0; i < program.Length; i++)
        {
            bus.Write((ushort)i, program[i]);
        }

        return (new DerivedCpu(StubIsaFile(), bus), bus);
    }

    [Fact]
    public void Derived_Removes_Opcode()
    {
        var (cpu, _) = Derived(0x00);
        FluentActions.Invoking(() => cpu.Step()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Derived_Replaces_Opcode()
    {
        var (cpu, _) = Derived(0x01, 0x05);
        cpu.Step().Should().Be(1, "podklasa nadpisała cykle");
        cpu.State.A.Should().Be(10, "podklasa nadpisała zachowanie");
    }

    [Fact]
    public void Derived_Adds_Opcode()
    {
        var (cpu, _) = Derived(0x01, 0x05, 0x07, 0x02);
        cpu.Step();
        cpu.Step();
        cpu.State.A.Should().Be(8);
    }

    [Fact]
    public void ResolveBehavior_Extends_Base()
    {
        const string json = """{"instructions":[{"opcode":"07","mnemonic":"SUB","cycles":2,"words":2}]}""";
        var bus = new StubBus();
        bus.Write(0, 0x07);
        bus.Write(1, 0x03);
        var cpu = new SubCpu(StubIsa.FromJson(new MemoryStream(Encoding.UTF8.GetBytes(json))), bus);
        cpu.State.A = 10;

        cpu.Step().Should().Be(2);
        cpu.State.A.Should().Be(7);
    }
}
