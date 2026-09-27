using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class AluTests
{
    [Theory]
    [InlineData(0x01, 0x02, 0x03, false, false)]
    [InlineData(0x7F, 0x01, 0x80, false, true)]
    [InlineData(0xFF, 0x01, 0x00, true, false)]
    [InlineData(0x80, 0x80, 0x00, true, true)]
    public void Add_Computes_Value_Carry_Overflow(int a, int b, int value, bool carry, bool overflow)
    {
        var result = Alu.Add((byte)a, (byte)b);

        result.Value.Should().Be((byte)value);
        result.Carry.Should().Be(carry);
        result.Overflow.Should().Be(overflow);
    }

    [Theory]
    [InlineData(0x00, false, true, false)]
    [InlineData(0x80, false, false, true)]
    [InlineData(0x7F, false, false, false)]
    public void Result_Derives_Zero_Negative(int value, bool carry, bool zero, bool negative)
    {
        var result = new AluResult((byte)value, carry, Overflow: false);

        result.Zero.Should().Be(zero);
        result.Negative.Should().Be(negative);
    }

    [Theory]
    [InlineData(0xFF, 0x00, 0x00, true, false)]
    [InlineData(0x7F, 0x00, 0x80, false, true)]
    public void Add_Honours_Carry_In(int a, int b, int value, bool carry, bool overflow)
    {
        var result = Alu.Add((byte)a, (byte)b, carryIn: true);

        result.Value.Should().Be((byte)value);
        result.Carry.Should().Be(carry);
        result.Overflow.Should().Be(overflow);
    }

    [Theory]
    [InlineData(0x05, 0x03, false, 0x02, true, false)]
    [InlineData(0x03, 0x05, false, 0xFE, false, false)]
    [InlineData(0x80, 0x01, false, 0x7F, true, true)]
    [InlineData(0x05, 0x02, true, 0x02, true, false)]
    [InlineData(0x00, 0x00, true, 0xFF, false, false)]
    public void Subtract_Computes_Value_Carry_Overflow(
        int a, int b, bool borrowIn, int value, bool carry, bool overflow)
    {
        var result = Alu.Subtract((byte)a, (byte)b, borrowIn);

        result.Value.Should().Be((byte)value);
        result.Carry.Should().Be(carry);
        result.Overflow.Should().Be(overflow);
    }
}
