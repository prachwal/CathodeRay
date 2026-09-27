using CathodeRay.Abstractions;

namespace CathodeRay.Stub;

/// <summary>Płaska pamięć RAM 64 KB jako <see cref="IBus"/> — wystarcza do testów abstrakcji.</summary>
public sealed class StubBus : IBus
{
    private readonly byte[] _memory = new byte[ushort.MaxValue + 1];

    /// <inheritdoc/>
    public byte Read(ushort address) => _memory[address];

    /// <inheritdoc/>
    public void Write(ushort address, byte value) => _memory[address] = value;
}
