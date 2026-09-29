namespace CathodeRay.C;

/// <summary>Cel kompilatora mini-C (jeden procesor): drukuje kod pośredni jako asembler tego CPU i dostarcza
/// wszystko, co jest specyficzne dla maszyny — start (crt0), biblioteki asemblerowe, układ pamięci i limit stosu.
/// Front-end nie zna flag, konwencji wołań, rozmieszczenia komórek ani kodowania instrukcji.</summary>
public interface ICTarget
{
    /// <summary>Nazwa w <c>cc --cpu</c>.</summary>
    string Name { get; }

    /// <summary>Opis do pomocy.</summary>
    string Description { get; }

    /// <summary>Nazwa celu asemblera (<c>AssemblerTargets</c>) do złożenia wyniku.</summary>
    string AssemblerCpu { get; }

    /// <summary>Kolejność bajtów słowa.</summary>
    TargetByteOrder ByteOrder { get; }

    /// <summary>Rozmiar stosu sprzętowego w bajtach do kontroli głębokości wołań (null = nie kontroluj).</summary>
    int? StackLimit { get; }

    /// <summary>Domyślny układ pamięci dla linkera.</summary>
    TargetLayout Layout { get; }

    /// <summary>Kod startowy (linkowany zawsze pierwszy): inicjalizacja, zerowanie BSS, wywołanie <c>main</c>.</summary>
    string Crt0 { get; }

    /// <summary>Moduły w asemblerze tego celu (konsola itp.), linkowane na żądanie razem z częścią C biblioteki.</summary>
    IReadOnlyList<StdModule> RuntimeModules { get; }

    /// <summary>Drukuje moduł jako tekst asemblera celu.</summary>
    /// <param name="module">Kod pośredni.</param>
    /// <param name="optimize">Włącz optymalizacje celu.</param>
    /// <returns>Źródło dla asemblera.</returns>
    string Emit(IrModule module, bool optimize);
}
