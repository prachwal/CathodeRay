namespace CathodeRay.Assembler;

/// <summary>Zakres segmentu w wyniku: adresy [Start, End), bajty emitowane w obrazie
/// (BSS nie emituje — adresy rosną, obrazu nie przybywa).</summary>
/// <param name="Name">Nazwa segmentu.</param>
/// <param name="Start">Pierwszy adres (włącznie).</param>
/// <param name="End">Adres za ostatnim (wyłącznie).</param>
/// <param name="Bss">Czy segment nie emituje bajtów.</param>
public sealed record SegmentSpan(string Name, int Start, int End, bool Bss);
