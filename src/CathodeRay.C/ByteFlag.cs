namespace CathodeRay.C;

/// <summary>Warunek skoku po ciągu SUB/CMP.</summary>
internal enum ByteFlag
{
    /// <summary>Wynik zero.</summary>
    Zero,

    /// <summary>Wynik niezerowy.</summary>
    NotZero,

    /// <summary>Pożyczka (lewy &lt; prawy bez znaku).</summary>
    Borrow,

    /// <summary>Brak pożyczki (lewy &gt;= prawy bez znaku).</summary>
    NoBorrow,
}
