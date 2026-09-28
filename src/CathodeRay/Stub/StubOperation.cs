namespace CathodeRay.Stub;

/// <summary>Wbudowane operacje zaślepki wykonywane przez <c>switch</c> w <see cref="StubCpu.Step"/> (bez delegatów).
/// Nazwa = mnemonik z JSON (bez rozróżniania wielkości liter). Nowa instrukcja: wartość tutaj + funkcja w <see cref="StubOps"/>
/// + gałąź w <c>StubCpu.Execute</c> + rodzaj w <c>StubCpu.Supports</c>.</summary>
public enum StubOperation
{
    /// <summary>Instrukcja spoza zestawu wbudowanego: wykonywana przez delegat z <see cref="StubOpcodeEntry.Handler"/>.</summary>
    Custom,

    /// <summary>Brak operacji.</summary>
    Nop,

    /// <summary>A = wartość (alias LDA dla trybu natychmiastowego).</summary>
    Ldi,

    /// <summary>A = wartość.</summary>
    Lda,

    /// <summary>X = wartość.</summary>
    Ldx,

    /// <summary>A = A + wartość.</summary>
    Add,

    /// <summary>A = A + wartość + C.</summary>
    Adc,

    /// <summary>A = A - wartość.</summary>
    Sub,

    /// <summary>Porównanie X z wartością.</summary>
    Cpx,

    /// <summary>A = A + 1.</summary>
    Inc,

    /// <summary>X = X + 1.</summary>
    Inx,

    /// <summary>M[adres] = A.</summary>
    Sta,

    /// <summary>PC = adres.</summary>
    Jmp,

    /// <summary>PC = adres, gdy Z = 0.</summary>
    Bne,

    /// <summary>X = A; ustawia Z.</summary>
    Tax,

    /// <summary>A = X; ustawia Z.</summary>
    Txa,

    /// <summary>A = A &amp; wartość; ustawia Z.</summary>
    And,

    /// <summary>A = A | wartość; ustawia Z.</summary>
    Ora,

    /// <summary>A = A ^ wartość; ustawia Z.</summary>
    Eor,

    /// <summary>A = A - 1; ustawia Z.</summary>
    Dec,

    /// <summary>X = X - 1; ustawia Z.</summary>
    Dex,

    /// <summary>PC = adres, gdy Z = 1.</summary>
    Beq,

    /// <summary>C = false.</summary>
    Clc,

    /// <summary>C = true.</summary>
    Sec,

    /// <summary>M[0100h + SP] = A; SP -= 1.</summary>
    Push,

    /// <summary>SP += 1; A = M[0100h + SP]; ustawia Z.</summary>
    Pop,

    /// <summary>Woła podprogram: odkłada PC, PC = adres.</summary>
    Call,

    /// <summary>Wraca z podprogramu: PC = odłożony adres.</summary>
    Ret,

    /// <summary>Zatrzymanie CPU.</summary>
    Hlt,
}
