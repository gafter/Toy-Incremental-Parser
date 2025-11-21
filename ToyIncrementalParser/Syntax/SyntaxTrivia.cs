using System;
using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax;

public sealed class SyntaxTrivia
{
    public SyntaxTrivia(NodeKind kind, string text, Range span, IReadOnlyList<Diagnostic>? diagnostics = null)
    {
        Kind = kind;
        Text = text;
        Span = span;
        Diagnostics = diagnostics ?? Array.Empty<Diagnostic>();
    }

    public NodeKind Kind { get; }
    public string Text { get; }
    public Range Span { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public override string ToString() => Text;
}

