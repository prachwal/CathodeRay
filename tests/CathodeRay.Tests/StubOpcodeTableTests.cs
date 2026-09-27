using CathodeRay.Abstractions;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class StubOpcodeTableTests
{
    private static readonly Action<OpcodeContext> Handler = _ => { };

    [Fact]
    public void Add_Duplicate_Throws()
    {
        var table = new StubOpcodeTable();
        table.Add(0x00, new StubOpcodeEntry(Handler, "NOP", 1, 1));

        FluentActions.Invoking(() => table.Add(0x00, new StubOpcodeEntry(Handler, "NOP", 1, 1)))
            .Should().Throw<InvalidOperationException>().WithMessage("*0x00*");
    }

    [Fact]
    public void Replace_Overwrites()
    {
        var table = new StubOpcodeTable();
        table.Add(0x01, new StubOpcodeEntry(Handler, "A", 1, 1));
        table.Replace(0x01, new StubOpcodeEntry(Handler, "B", 2, 2));

        table.Get(0x01).Mnemonic.Should().Be("B");
        table.Get(0x01).Cycles.Should().Be(2);
    }

    [Fact]
    public void Seal_Blocks_Mutation()
    {
        StubOpcodeTable table = new StubOpcodeTable().Seal();
        table.IsSealed.Should().BeTrue();

        FluentActions.Invoking(() => table.Add(0x02, new StubOpcodeEntry(Handler, "C", 1, 1)))
            .Should().Throw<InvalidOperationException>().WithMessage("*sealed*");
    }

    [Fact]
    public void Get_Missing_Throws()
    {
        var table = new StubOpcodeTable();

        FluentActions.Invoking(() => table.Get(0x7F)).Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void TryGet_Reports_Presence()
    {
        var table = new StubOpcodeTable();
        table.Add(0x03, new StubOpcodeEntry(Handler, "INC", 1, 1));

        table.TryGet(0x03, out StubOpcodeEntry? found).Should().BeTrue();
        found!.Mnemonic.Should().Be("INC");
        table.TryGet(0x04, out _).Should().BeFalse();
    }
}
