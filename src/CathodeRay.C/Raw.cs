namespace CathodeRay.C;

/// <summary>Fragment tekstu asemblera celu (migracja: generator jeszcze pisze mnemoniki stuba).
/// Zawiera dokładny tekst razem z końcem linii, więc wydruk to zwykłe złączenie.</summary>
/// <param name="Text">Tekst fragmentu.</param>
public sealed record Raw(string Text) : Ins;
