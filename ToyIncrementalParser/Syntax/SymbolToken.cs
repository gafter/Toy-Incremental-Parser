using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

internal readonly struct SymbolToken
{
    public SymbolToken(GreenToken token, int fullStart, int spanStart)
    {
        Token = token;
        FullStart = fullStart;
        SpanStart = spanStart;
    }

    public GreenToken Token { get; }

    public int FullStart { get; }

    public int SpanStart { get; }

    public int FullEnd => FullStart + Token.FullWidth;
}
