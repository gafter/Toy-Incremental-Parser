using System;

namespace ToyIncrementalParser.Syntax;

public sealed class SyntaxTrivia
{
    public SyntaxTrivia(NodeKind kind, string text, Range span)
    {
        Kind = kind;
        Text = text;
        Span = span;
    }

    public NodeKind Kind { get; }
    public string Text { get; }
    public Range Span { get; }

    public override string ToString() => Text;
}

