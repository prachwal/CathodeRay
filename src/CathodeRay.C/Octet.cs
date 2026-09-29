namespace CathodeRay.C;

/// <summary>Bajt operandu: stała albo komórka pamięci (adres jako tekst asemblera).</summary>
/// <param name="IsImmediate">Stała.</param>
/// <param name="Text">Wartość albo wyrażenie adresu.</param>
internal readonly record struct Octet(bool IsImmediate, string Text);
