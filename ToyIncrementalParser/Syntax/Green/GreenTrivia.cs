namespace ToyIncrementalParser.Syntax.Green;

internal sealed class GreenTrivia
{
    public GreenTrivia(NodeKind kind, string text)
    {
        Kind = kind;
        Text = text;
        FullWidth = text.Length;
    }

    public NodeKind Kind { get; }

    public string Text { get; }

    public int FullWidth { get; }
}

