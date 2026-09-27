using CathodeRay.Abstractions;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class StubOpsTests
{
    [Fact]
    public void Ldi_Loads_A_Without_Touching_Flags()
    {
        var state = new StubState { Carry = true, Overflow = true };

        StubOps.Ldi(state, 0x42);

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
    public void Sta_Writes_A_To_Bus()
    {
        var state = new StubState { A = 0x99 };
        var bus = new StubBus();

        StubOps.Sta(state, bus, 0x2000).Should().Be(BusActivity.Write);

        bus.Read(0x2000).Should().Be(0x99);
    }

    [Fact]
    public void Lda_Reads_A_From_Bus()
    {
        var state = new StubState();
        var bus = new StubBus();
        bus.Write(0x3000, 0x5A);

        StubOps.Lda(state, bus, 0x3000).Should().Be(BusActivity.Read);

        state.A.Should().Be(0x5A);
    }

    [Fact]
    public void Hlt_Halts()
    {
        var state = new StubState();

        StubOps.Hlt(state).Should().Be(BusActivity.Halt);

        state.Halted.Should().BeTrue();
    }
}
