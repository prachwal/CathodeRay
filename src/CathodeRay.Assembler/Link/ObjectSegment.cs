namespace CathodeRay.Assembler.Link;

/// <summary>Segment w module obiektu.</summary>
/// <param name="Name">Nazwa.</param>
/// <param name="Bss">Czy nie emituje bajtów.</param>
/// <param name="Data">Bajty (base64 w JSON); puste dla BSS.</param>
/// <param name="Length">Długość w adresach (dla BSS; inaczej <c>Data.Length</c>).</param>
public sealed record ObjectSegment(string Name, bool Bss, byte[] Data, int Length);
