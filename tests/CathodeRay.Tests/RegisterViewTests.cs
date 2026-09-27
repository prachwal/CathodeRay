using CathodeRay.Abstractions;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class RegisterViewTests
{
    private static RegisterView Sample() => new(
    [
        new RegisterEntry("A", 0x1F, 8),
        new RegisterEntry("PC", 0x1234, 16),
        new RegisterEntry("Nibble", 0xA, 4),
    ]);

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
        FluentActions.Invoking(() => new RegisterView(
            [new RegisterEntry("A", 1, 8), new RegisterEntry("A", 2, 8)]))
            .Should().Throw<ArgumentException>().WithMessage("*A*");
    }

    [Fact]
    public void Empty_Has_No_Registers()
    {
        RegisterView.Empty.Count.Should().Be(0);
        RegisterView.Empty.EightBitNames.Should().BeEmpty();
    }
}
