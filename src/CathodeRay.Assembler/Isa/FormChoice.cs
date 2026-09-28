namespace CathodeRay.Assembler.Isa;

/// <summary>Wybrana forma instrukcji z tekstami wyrażeń dla jej pól.</summary>
internal readonly record struct FormChoice(InstructionForm Form, string[] Captures);
