namespace CathodeRay.C;

/// <summary>CPU potrafi wywołanie ogonowe (skok zamiast call+ret), także przez wskaźnik.</summary>
internal interface ITailCall
{
    /// <summary>Czy cel obsługuje wywołanie ogonowe (skok zamiast call+ret).</summary>
    bool SupportsTailCall { get; }

    /// <summary>Skok pośredni w pozycji ogonowej (wskaźnik w komórce 2-bajtowej).</summary>
    /// <param name="cell">Symbol komórki z adresem.</param>
    void TailCallIndirect(string cell);
}
