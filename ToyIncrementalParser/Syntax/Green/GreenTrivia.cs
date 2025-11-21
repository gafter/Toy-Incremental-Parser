using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax.Green;

internal sealed class GreenTrivia
{
    public GreenTrivia(NodeKind kind, string text, IReadOnlyList<Diagnostic>? diagnostics = null)
    {
        Kind = kind;
        Text = text;
        FullWidth = text.Length;
        Diagnostics = diagnostics ?? System.Array.Empty<Diagnostic>();
    }

    public NodeKind Kind { get; }

    public string Text { get; }

    public int FullWidth { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}
