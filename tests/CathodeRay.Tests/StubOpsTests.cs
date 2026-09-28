using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class StubOpsTests
{
    [Fact]
    public void Lda_Loads_A_Without_Touching_Carry_Or_Overflow()
    {
        var state = new StubState { Carry = true, Overflow = true };

        StubOps.Lda(state, 0x42);

        state.A.Should().Be(0x42);
        state.Carry.Should().BeTrue();
        state.Overflow.Should().BeTrue();
    }

    [Theory]
    [InlineData(0x01, 0x02, 0x03, false, false)]
    [InlineData(0xFF, 0x01, 0x00, true, false)]
    [InlineData(0x7F, 0x01, 0x80, false, true)]
    public void Add_Sets_A_And_Flags(int a, int operand, int expected, bool carry, bool overflow)
    {
        var state = new StubState { A = (byte)a };

        StubOps.Add(state, (byte)operand);

        state.A.Should().Be((byte)expected);
        state.Carry.Should().Be(carry);
        state.Overflow.Should().Be(overflow);
    }

    [Fact]
    public void Add_Ignores_Previous_Carry()
    {
        var state = new StubState { A = 0x01, Carry = true };

        StubOps.Add(state, 0x01);

        state.A.Should().Be(0x02);
        state.Carry.Should().BeFalse();
    }

    [Theory]
    [InlineData(0x05, 0x03, 0x02, true, false)]
    [InlineData(0x03, 0x05, 0xFE, false, false)]
    [InlineData(0x80, 0x01, 0x7F, true, true)]
    public void Sub_Sets_A_And_Flags(int a, int operand, int expected, bool carry, bool overflow)
    {
        var state = new StubState { A = (byte)a };

        StubOps.Sub(state, (byte)operand);

        state.A.Should().Be((byte)expected);
        state.Carry.Should().Be(carry);
        state.Overflow.Should().Be(overflow);
    }

    [Theory]
    [InlineData(0x00, 0x01)]
    [InlineData(0xFF, 0x00)]
    public void Inc_Wraps_And_Preserves_Flags(int a, int expected)
    {
        var state = new StubState { A = (byte)a, Carry = true, Overflow = true };

        StubOps.Inc(state);

        state.A.Should().Be((byte)expected);
        state.Carry.Should().BeTrue();
        state.Overflow.Should().BeTrue();
    }

    [Fact]
    public void Jmp_Sets_ProgramCounter()
    {
        var state = new StubState();

        StubOps.Jmp(state, 0x1234);

        state.ProgramCounter.Should().Be(0x1234);
    }

    [Fact]
    public void Hlt_Halts()
    {
        var state = new StubState();

        StubOps.Hlt(state);

        state.Halted.Should().BeTrue();
    }

    [Theory]
    [InlineData(0x00, true)]
    [InlineData(0x01, false)]
    public void Lda_Sets_Zero(int value, bool zero)
    {
        var state = new StubState { Zero = !zero };

        StubOps.Lda(state, (byte)value);

        state.Zero.Should().Be(zero);
    }

    [Fact]
    public void Arithmetic_Sets_Zero()
    {
        var state = new StubState { A = 0xFF };
        StubOps.Add(state, 1);
        state.Zero.Should().BeTrue();

        StubOps.Sub(state, 0xFF);
        state.Zero.Should().BeFalse();

        state.A = 0xFF;
        StubOps.Inc(state);
        state.Zero.Should().BeTrue();
    }

    [Theory]
    [InlineData(0x01, false, 0x01, 0x02, false)]
    [InlineData(0x01, true, 0x01, 0x03, false)]
    [InlineData(0xFF, true, 0x00, 0x00, true)]
    public void Adc_Adds_Carry_In(int a, bool carryIn, int operand, int expected, bool carryOut)
    {
        var state = new StubState { A = (byte)a, Carry = carryIn };

        StubOps.Adc(state, (byte)operand);

        state.A.Should().Be((byte)expected);
        state.Carry.Should().Be(carryOut);
    }

    [Theory]
    [InlineData(0x00, true)]
    [InlineData(0x07, false)]
    public void Ldx_Loads_X_And_Sets_Zero(int value, bool zero)
    {
        var state = new StubState { A = 0x55 };

        StubOps.Ldx(state, (byte)value);

        state.X.Should().Be((byte)value);
        state.Zero.Should().Be(zero);
        state.A.Should().Be(0x55);
    }

    [Theory]
    [InlineData(0x00, 0x01, false)]
    [InlineData(0xFF, 0x00, true)]
    public void Inx_Wraps_And_Sets_Zero(int x, int expected, bool zero)
    {
        var state = new StubState { X = (byte)x, Carry = true };

        StubOps.Inx(state);

        state.X.Should().Be((byte)expected);
        state.Zero.Should().Be(zero);
        state.Carry.Should().BeTrue();
    }

    [Theory]
    [InlineData(5, 5, true, true)]
    [InlineData(6, 5, false, true)]
    [InlineData(4, 5, false, false)]
    public void Cpx_Compares_Without_Changing_X_Or_Overflow(int x, int value, bool zero, bool carry)
    {
        var state = new StubState { X = (byte)x, Overflow = true };

        StubOps.Cpx(state, (byte)value);

        state.Zero.Should().Be(zero);
        state.Carry.Should().Be(carry);
        state.X.Should().Be((byte)x);
        state.Overflow.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, 0x1234)]
    [InlineData(true, 0x0010)]
    public void Bne_Branches_Only_When_Not_Zero(bool zero, int expectedPc)
    {
        var state = new StubState { Zero = zero, ProgramCounter = 0x0010 };

        StubOps.Bne(state, 0x1234);

        state.ProgramCounter.Should().Be((ushort)expectedPc);
    }
}
