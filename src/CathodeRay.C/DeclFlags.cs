namespace CathodeRay.C;

/// <summary>Modyfikatory deklaracji zmiennej lub funkcji.</summary>
[Flags]
public enum DeclFlags
{
    /// <summary>Brak.</summary>
    None = 0,

    /// <summary><c>static</c>: symbol lokalny modułu; dla zmiennej w funkcji — jedna komórka na cały program.</summary>
    Static = 1,

    /// <summary><c>extern</c>: deklaracja bez miejsca (definicja w innym module).</summary>
    Extern = 2,
}
