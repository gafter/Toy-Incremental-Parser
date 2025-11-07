using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public sealed class SyntaxTrivia
{
    public SyntaxTrivia(NodeKind kind, string text, TextSpan span)
    {
        Kind = kind;
        Text = text;
        Span = span;
    }

    public NodeKind Kind { get; }
    public string Text { get; }
    public TextSpan Span { get; }

    public override string ToString() => Text;
}

