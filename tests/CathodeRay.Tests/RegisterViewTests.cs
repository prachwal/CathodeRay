using CathodeRay.Abstractions;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class RegisterViewTests
{
    private static readonly RegisterDefinition DefA = new("A", 8, RegisterRole.Accumulator);
    private static readonly RegisterDefinition DefPc = new("PC", 16, RegisterRole.ProgramCounter);
    private static readonly RegisterDefinition DefNibble = new("Nibble", 4, RegisterRole.General);

    private static readonly RegisterLayout Layout = new(DefA, DefPc, DefNibble);

    private static RegisterView Sample() => new(Layout, [0x1F, 0x1234, 0xA]);

    [Fact]
    public void Exposes_Names_And_Lookup()
    {
        RegisterView view = Sample();

        view.Count.Should().Be(3);
        view.Names.Should().Equal("A", "PC", "Nibble");
        view["A"].Value.Should().Be(0x1F);
        view.TryGet("PC", out RegisterEntry pc).Should().BeTrue();
        pc.WidthBits.Should().Be(16);
        view.TryGet("X", out _).Should().BeFalse();
    }

    [Fact]
    public void Carries_Role_From_Definition()
    {
        Sample()["PC"].Role.Should().Be(RegisterRole.ProgramCounter);
        Sample()["A"].Definition.Role.Should().Be(RegisterRole.Accumulator);
    }

    [Fact]
    public void Reports_EightBit_Names()
    {
        Sample().EightBitNames.Should().Equal("A");
    }

    [Fact]
    public void Formats_Value_By_Width()
    {
        Sample()["A"].Format().Should().Be("1F");
        Sample()["PC"].Format().Should().Be("1234");
        Sample()["Nibble"].Format().Should().Be("A");
    }

    [Fact]
    public void Duplicate_Name_Throws()
    {
        FluentActions.Invoking(() => new RegisterLayout(DefA, DefA))
            .Should().Throw<ArgumentException>().WithMessage("*A*");
    }

    [Fact]
    public void Value_Count_Must_Match_Layout()
    {
        FluentActions.Invoking(() => new RegisterView(Layout, [1, 2]))
            .Should().Throw<ArgumentException>().WithMessage("*3*2*");
    }

    [Fact]
    public void Unknown_Name_Throws_On_Indexer()
    {
        FluentActions.Invoking(() => Sample()["X"]).Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Enumerates_Entries_In_Layout_Order()
    {
        Sample().Select(e => (e.Name, e.Value)).Should().Equal(("A", 0x1FUL), ("PC", 0x1234UL), ("Nibble", 0xAUL));
    }

    [Fact]
    public void Views_Share_Layout_But_Not_Values()
    {
        var first = new RegisterView(Layout, [1, 2, 3]);
        var second = new RegisterView(Layout, [4, 5, 6]);

        first.Names.Should().BeSameAs(second.Names);
        first["A"].Value.Should().Be(1);
        second["A"].Value.Should().Be(4);
    }

    [Fact]
    public void Empty_Has_No_Registers()
    {
        RegisterView.Empty.Count.Should().Be(0);
        RegisterView.Empty.EightBitNames.Should().BeEmpty();
    }
}
