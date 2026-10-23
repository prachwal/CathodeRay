using System.Globalization;
using System.Text;

namespace CathodeRay.C;

/// <summary>Czytelny tekst kodu pośredniego (do diagnostyki, nie do ponownego wczytania).</summary>
public static class IrFormatter
{
    /// <summary>Formatuje moduł: funkcje z numerowanymi instrukcjami, potem dane.</summary>
    /// <param name="module">Moduł IR.</param>
    /// <returns>Tekst wielowierszowy.</returns>
    public static string Format(Ir.Module module)
    {
        ArgumentNullException.ThrowIfNull(module);
        var text = new StringBuilder();
        foreach (Ir.Function function in module.Functions)
        {
            string parameters = string.Join(", ", function.Params.Select(Op));
            text.AppendLine(CultureInfo.InvariantCulture, $"func {(function.IsStatic ? "static " : string.Empty)}{function.Name}({parameters}) ret={function.RetW}B");
            Ir.Src? lastSource = null;
            for (int i = 0; i < function.Body.Count; i++)
            {
                Ir.Ins ins = function.Body[i];
                if (ins is Ir.Src source)
                {
                    if (source == lastSource)
                    {
                        continue;
                    }

                    lastSource = source;
                }

                text.AppendLine(CultureInfo.InvariantCulture, $"  {(ins is Ir.Label ? string.Empty : "  ")}{i,4}: {Ins(ins)}");
            }

            text.AppendLine();
        }

        foreach (Ir.Data data in module.Data)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"data {data.Sym} {data.Segment} {data.Size}B{(data.Exported ? " exported" : string.Empty)}");
        }

        if (module.ExternFunctions.Count > 0)
        {
            text.AppendLine("extern func " + string.Join(", ", module.ExternFunctions));
        }

        if (module.ExternCells.Count > 0)
        {
            text.AppendLine("extern cell " + string.Join(", ", module.ExternCells));
        }

        return text.ToString();
    }

    private static string Op(Ir.Op op) => op switch
    {
        Ir.Cell c => $"{c.Sym}:{c.W}",
        Ir.Imm i => i.High != 0 ? $"#{i.Value}/{i.High}:{i.W}" : $"#{i.Value}:{i.W}",
        Ir.AddrOf a => a.Off == 0 ? $"&{a.Sym}" : $"&{a.Sym}{a.Off:+0;-0}",
        _ => op.ToString() ?? "?",
    };

    private static string Ins(Ir.Ins ins) => ins switch
    {
        Ir.Mov m => $"{Op(m.Dst)} = {Op(m.Src)}",
        Ir.Bin b => $"{Op(b.Dst)} = {b.Kind} {Op(b.A)}, {Op(b.B)}",
        Ir.Un u => $"{Op(u.Dst)} = {u.Kind} {Op(u.A)}",
        Ir.Load l => $"{Op(l.Dst)} = load{l.Bytes}{(l.Volatile ? " volatile" : string.Empty)} [{Op(l.Ptr)}{l.Off:+0;-0;}]",
        Ir.Store s => $"store{s.Bytes}{(s.Volatile ? " volatile" : string.Empty)} [{Op(s.Ptr)}{s.Off:+0;-0;}] = {Op(s.Value)}",
        Ir.LoadIdx l => $"{Op(l.Dst)} = loadidx{l.Bytes} {l.Sym}{l.Off:+0;-0;}[{Op(l.Index)} << {l.Shift}]",
        Ir.StoreIdx s => $"storeidx{s.Bytes} {s.Sym}{s.Off:+0;-0;}[{Op(s.Index)} << {s.Shift}] = {Op(s.Value)}",
        Ir.CopyBlock c => $"copy {Op(c.Dst)} <- {Op(c.Src)}, {c.Size}B",
        Ir.Fill f => $"fill {Op(f.Dst)}, {f.Value}, {f.Size}B",
        Ir.BrCmp b => $"br {b.C} {Op(b.A)}, {Op(b.B)} -> {b.Target}",
        Ir.Jmp j => $"jmp {j.Target}",
        Ir.Label l => $"{l.Name}:",
        Ir.Call c => $"{(c.Result is null ? string.Empty : Op(c.Result) + " = ")}call {c.Direct ?? "*" + Op(c.Indirect!)}({string.Join(", ", c.Args.Select(Op))})",
        Ir.Ret r => r.Value is null ? "ret" : $"ret {Op(r.Value)}",
        Ir.Src s => $";c: {s.File}:{s.Line}",
        _ => ins.ToString() ?? "?",
    };
}
