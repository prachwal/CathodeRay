namespace CathodeRay.C;

/// <summary>Kwalifikatory <c>const</c> i <c>volatile</c> w nazwach typów z parsera (<c>"const volatile uchar"</c>).</summary>
internal static class TypeQualifiers
{
    /// <summary>Odcina kwalifikatory z początku nazwy typu.</summary>
    /// <param name="type">Nazwa z prefiksami.</param>
    /// <param name="isConst">Był <c>const</c>.</param>
    /// <param name="isVolatile">Był <c>volatile</c>.</param>
    /// <returns>Nazwa bez kwalifikatorów.</returns>
    public static string Split(string type, out bool isConst, out bool isVolatile)
    {
        isConst = false;
        isVolatile = false;
        string rest = type;
        while (true)
        {
            if (rest.StartsWith("const ", StringComparison.Ordinal))
            {
                isConst = true;
                rest = rest[6..];
            }
            else if (rest.StartsWith("volatile ", StringComparison.Ordinal))
            {
                isVolatile = true;
                rest = rest[9..];
            }
            else
            {
                return rest;
            }
        }
    }

    /// <summary>Składa nazwę z kwalifikatorami w stałej kolejności (<c>const</c>, potem <c>volatile</c>).</summary>
    /// <param name="bare">Nazwa bez kwalifikatorów.</param>
    /// <param name="isConst">Dodaj <c>const</c>.</param>
    /// <param name="isVolatile">Dodaj <c>volatile</c>.</param>
    /// <returns>Nazwa z prefiksami.</returns>
    public static string Join(string bare, bool isConst, bool isVolatile) =>
        (isConst ? "const " : string.Empty) + (isVolatile ? "volatile " : string.Empty) + bare;
}
