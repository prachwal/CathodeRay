namespace CathodeRay.Assembler.Link;

/// <summary>Symbol w module obiektu.</summary>
/// <param name="Name">Nazwa.</param>
/// <param name="Segment">Segment definicji.</param>
/// <param name="Offset">Offset w segmencie.</param>
/// <param name="Global">Czy wyeksportowany (<c>GLOBAL</c>).</param>
public sealed record ObjectSymbol(string Name, string Segment, int Offset, bool Global);
