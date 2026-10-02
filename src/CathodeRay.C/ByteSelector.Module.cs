using System.Globalization;
using System.Text;

namespace CathodeRay.C;

internal sealed partial class ByteSelector
{
    private string Header()
    {
        var text = new StringBuilder();
        text.AppendLine(_isa.Segment("CODE"));
        if (!_module.ObjectMode)
        {
            return text.ToString();
        }

        text.Append((_isa as IPreamble)?.Preamble() ?? string.Empty);
        foreach (string function in _module.ExternFunctions)
        {
            text.AppendLine(_isa.Extern(_isa.Sym(function)));
        }

        foreach (string external in _module.ExternCells)
        {
            text.AppendLine(_isa.Extern(_isa.Sym(external)));
        }

        for (int arg = 1; arg <= TypeChecker.MaxArgs; arg++)
        {
            text.AppendLine(_isa.Extern($"cc_arg{arg}"));
            text.AppendLine(_isa.Extern($"cc_arg{arg}_h"));
        }

        foreach (string symbol in new[] { "cc_ret", "cc_ret_h", "cc_t0", "cc_t1" })
        {
            text.AppendLine(_isa.Extern(symbol));
        }

        if (_usesIcall)
        {
            foreach (string symbol in _isa.IndirectSymbols)
            {
                text.AppendLine(_isa.Extern(symbol));
            }
        }

        return text.ToString();
    }

    private string PrintInit()
    {
        var text = new StringBuilder();
        Ir.Data[] table = [.. _module.Data.Where(static d => d.Segment == "INIT")];
        if (table.Length == 0 && _module.ObjectMode)
        {
            return string.Empty;
        }

        text.AppendLine(_isa.Segment("INIT"));
        if (!_module.ObjectMode)
        {
            text.AppendLine("__init_start:");
        }

        foreach (Ir.Data data in table)
        {
            AppendData(text, data);
        }

        if (!_module.ObjectMode)
        {
            text.AppendLine("__init_end:");
        }

        return text.ToString();
    }

    private string PrintData()
    {
        var text = new StringBuilder();
        text.AppendLine(_isa.Segment("DATA"));
        foreach (Ir.Data data in _module.Data.Where(static d => d.Segment == "DATA"))
        {
            AppendData(text, data);
        }

        foreach ((string label, string expression) in _addressList)
        {
            text.AppendLine($"{label}: {_isa.Word(expression)}");
        }

        return text.ToString();
    }

    private string PrintBss()
    {
        var text = new StringBuilder();
        foreach (string segment in new[] { "ZP", "BSS" })
        {
            AppendReserved(text, segment);
        }

        return text.ToString();
    }

    private void AppendReserved(StringBuilder text, string segment)
    {
        if (segment == "ZP" && !_module.Data.Any(static d => d.Segment == "ZP") && _module.ObjectMode)
        {
            return;
        }

        text.AppendLine(_isa.Segment(segment));
        foreach (Ir.Data data in _module.Data.Where(d => d.Segment == segment && !_isa.Cells.IsRegister(_isa.Sym(d.Sym))))
        {
            if (data.Exported)
            {
                text.AppendLine(_isa.Global(_isa.Sym(data.Sym)));
            }

            text.AppendLine($"{_isa.Sym(data.Sym)}: {_isa.Reserve(data.Size)}");
        }

        if (!_module.ObjectMode && segment == "BSS")
        {
            text.AppendLine("__bss_end:");
        }
        else if (!_module.ObjectMode && segment == "ZP")
        {
            text.AppendLine("__zp_end:");
        }
    }

    private void AppendData(StringBuilder text, Ir.Data data)
    {
        if (data.Exported)
        {
            text.AppendLine(_isa.Global(_isa.Sym(data.Sym)));
        }

        string label = data.Sym.Length == 0 ? string.Empty : $"{_isa.Sym(data.Sym)}: ";
        foreach (Ir.Piece piece in data.Init!)
        {
            switch (piece)
            {
                case Ir.Bytes bytes:
                    text.AppendLine($"{label}{_isa.Bytes(bytes.Value.Select(static b => (int)b))}");
                    break;
                case Ir.SymWord word:
                    text.AppendLine($"{label}{_isa.Word(At(word.Sym, word.Off))}");
                    break;
            }

            label = string.Empty;
        }
    }
}
