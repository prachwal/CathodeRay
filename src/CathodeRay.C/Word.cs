namespace CathodeRay.C;

/// <summary>Słowo 16-bitowe operandu: stała (liczba albo adres symbolu, całe słowo w <paramref name="Lo"/>) albo dwa bajty pamięci.</summary>
/// <param name="IsImmediate">Stała.</param>
/// <param name="Lo">Wyrażenie stałej albo adres młodszego bajtu.</param>
/// <param name="Hi">Adres starszego bajtu (pusty dla stałej).</param>
internal readonly record struct Word(bool IsImmediate, string Lo, string Hi);
